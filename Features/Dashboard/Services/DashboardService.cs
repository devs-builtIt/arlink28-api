using Arlink28.Api.Data;
using Arlink28.Api.Data.Entities;
using Arlink28.Api.Features.Dashboard.ResponseModels;
using Arlink28.Api.Features.Dashboard.Services.Interfaces;
using Arlink28.Api.Features.Shared.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Arlink28.Api.Features.Dashboard.Services;

public class DashboardService(ApplicationDbContext db) : IDashboardService, IScoped
{
    public const int MinDays = 7;
    public const int MaxDays = 365;
    private const int ListLength = 5;

    public async Task<DashboardResponse> GetAsync(int days, CancellationToken ct = default)
    {
        days = Math.Clamp(days, MinDays, MaxDays);
        var now = DateTime.UtcNow;
        var today = DateOnly.FromDateTime(now);
        var from = today.AddDays(-(days - 1));
        var previousFrom = from.AddDays(-days);

        return new DashboardResponse(now, days,
            await EnquiryMetricsAsync(from, previousFrom, days, ct),
            await CatalogueMetricsAsync(ct));
    }

    private async Task<EnquiryMetrics> EnquiryMetricsAsync(DateOnly from, DateOnly previousFrom, int days, CancellationToken ct)
    {
        var windowStart = previousFrom.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);

        // Two periods' worth of enquiries is small, so the grouping is done here rather than in SQL.
        var rows = await db.Enquiries.AsNoTracking()
            .Where(e => e.CreatedAt >= windowStart)
            .Select(e => new Row(e.Id, e.Type, e.Status, e.PackageId, e.PackageTitle,
                e.Package != null ? e.Package.ProductType : (ProductType?)null,
                e.QuotedTotalMinor, e.Currency, e.CreatedAt, e.UpdatedAt, e.HandledById,
                e.Package != null ? e.Package.Destination.Name : null,
                e.HandledBy != null ? e.HandledBy.Username : null))
            .ToListAsync(ct);

        var current = rows.Where(r => DateOnly.FromDateTime(r.CreatedAt) >= from).ToList();
        var previous = rows.Where(r => DateOnly.FromDateTime(r.CreatedAt) < from).ToList();

        var awaitingAll = await db.Enquiries.CountAsync(e => e.Status == EnquiryStatus.New, ct);
        var awaiting = await db.Enquiries.AsNoTracking()
            .Where(e => e.Status == EnquiryStatus.New)
            .OrderBy(e => e.CreatedAt).Take(ListLength)
            .Select(e => new AwaitingEnquiry(e.Id, e.Reference, e.Name, e.Type, e.PackageTitle, e.CreatedAt))
            .ToListAsync(ct);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var upcoming = await db.Enquiries.AsNoTracking()
            .Where(e => e.CheckIn != null && e.CheckIn >= today && e.Status != EnquiryStatus.Closed)
            .OrderBy(e => e.CheckIn).ThenBy(e => e.CreatedAt).Take(ListLength)
            .Select(e => new UpcomingTrip(e.Id, e.Reference, e.Name, e.PackageTitle, e.CheckIn!.Value, e.Nights, e.Status))
            .ToListAsync(ct);
        var inThirty = today.AddDays(30);
        var upcomingNext30 = await db.Enquiries.CountAsync(
            e => e.CheckIn != null && e.CheckIn >= today && e.CheckIn <= inThirty && e.Status != EnquiryStatus.Closed, ct);

        return new EnquiryMetrics(
            Total: current.Count,
            PreviousTotal: previous.Count,
            New: current.Count(r => r.Status == EnquiryStatus.New),
            Contacted: current.Count(r => r.Status == EnquiryStatus.Contacted),
            Closed: current.Count(r => r.Status == EnquiryStatus.Closed),
            AwaitingAll: awaitingAll,
            MedianHandleHours: MedianHandleHours(current),
            Series: Series(current, from, days, 0),
            PreviousSeries: Series(previous, previousFrom, days, days),
            ByType: current.GroupBy(r => r.Type).OrderByDescending(g => g.Count()).ThenBy(g => g.Key)
                .Select(g => new TypeCount(g.Key, g.Count())).ToList(),
            ByProduct: current.GroupBy(r => r.Product?.ToString() ?? "General").OrderByDescending(g => g.Count())
                .ThenBy(g => g.Key).Select(g => new ProductCount(g.Key, g.Count())).ToList(),
            ByWeekday: Weekdays(current),
            Quoted: current.Where(r => r.QuotedMinor is > 0 && !string.IsNullOrEmpty(r.Currency))
                .GroupBy(r => r.Currency!).OrderByDescending(g => g.Sum(r => r.QuotedMinor!.Value))
                .Select(g => new CurrencyTotal(g.Key, g.Sum(r => r.QuotedMinor!.Value), g.Count())).ToList(),
            TopPackages: current.Where(r => !string.IsNullOrEmpty(r.PackageTitle))
                .GroupBy(r => new { r.PackageId, Title = r.PackageTitle! })
                .OrderByDescending(g => g.Count()).ThenBy(g => g.Key.Title).Take(ListLength)
                .Select(g => new PackageCount(g.Key.PackageId, g.Key.Title, g.Count())).ToList(),
            Awaiting: awaiting,
            ByDestination: current.Where(r => r.Destination is not null).GroupBy(r => r.Destination!)
                .OrderByDescending(g => g.Count()).ThenBy(g => g.Key).Take(6)
                .Select(g => new DestinationCount(g.Key, g.Count())).ToList(),
            Staff: current.Where(r => r.HandledById is not null && r.Status != EnquiryStatus.New)
                .GroupBy(r => new { Id = r.HandledById!.Value, Name = r.HandledByName ?? "Former staff" })
                .OrderByDescending(g => g.Count()).ThenBy(g => g.Key.Name).Take(ListLength)
                .Select(g => new StaffWorkload(g.Key.Id, g.Key.Name, g.Count(), g.Count(r => r.Status == EnquiryStatus.Closed)))
                .ToList(),
            Upcoming: upcoming,
            UpcomingNext30: upcomingNext30);
    }

    /// <summary>
    /// Arrival to the last update, for enquiries a staff member has acted on. An enquiry keeps no history of its
    /// status changes, so for one that moved on more than once this is the time to the last move, not the first.
    /// </summary>
    private static double? MedianHandleHours(List<Row> current)
    {
        var hours = current.Where(r => r.Status != EnquiryStatus.New && r.HandledById is not null)
            .Select(r => Math.Max(0, (r.UpdatedAt - r.CreatedAt).TotalHours)).Order().ToList();
        if (hours.Count == 0) return null;
        var mid = hours.Count / 2;
        var median = hours.Count % 2 == 1 ? hours[mid] : (hours[mid - 1] + hours[mid]) / 2;
        return Math.Round(median, 1);
    }

    /// <summary>One entry per day, zero when nothing came in. Dates are shifted forward by <paramref name="shift"/> days.</summary>
    private static List<DayCount> Series(List<Row> rows, DateOnly start, int days, int shift)
    {
        var counts = rows.GroupBy(r => DateOnly.FromDateTime(r.CreatedAt)).ToDictionary(g => g.Key, g => g.Count());
        return Enumerable.Range(0, days).Select(i =>
        {
            var day = start.AddDays(i);
            return new DayCount(day.AddDays(shift), counts.GetValueOrDefault(day));
        }).ToList();
    }

    private static List<int> Weekdays(List<Row> rows)
    {
        var result = new int[7];
        foreach (var r in rows) result[((int)r.CreatedAt.DayOfWeek + 6) % 7]++; // Monday = 0
        return [.. result];
    }

    private async Task<CatalogueMetrics> CatalogueMetricsAsync(CancellationToken ct)
    {
        var packages = await db.Packages.AsNoTracking()
            .Select(p => new
            {
                p.Id, p.Title, p.ProductType, p.Status, p.Featured,
                HasPhoto = p.Media.Any(m => m.Role != MediaRole.Poster),
                HasPrice = p.FromPriceMinor != null,
                HasSummary = p.Summary != null && p.Summary != "",
            }).ToListAsync(ct);

        var byProduct = packages.GroupBy(p => p.ProductType).OrderBy(g => g.Key)
            .Select(g => new ProductStatusCount(g.Key.ToString(),
                g.Count(p => p.Status == PackageStatus.Published),
                g.Count(p => p.Status == PackageStatus.Draft),
                g.Count(p => p.Status == PackageStatus.Archived))).ToList();

        // Published listings that would look unfinished to a guest, with the first thing to fix.
        var toFix = packages.Where(p => p.Status == PackageStatus.Published)
            .Select(p => new
            {
                p.Id, p.Title, p.ProductType,
                Reason = !p.HasPhoto ? "No photo" : !p.HasSummary ? "No summary" : !p.HasPrice ? "No price" : null,
            })
            .Where(p => p.Reason is not null).ToList();

        return new CatalogueMetrics(
            byProduct,
            Featured: packages.Count(p => p.Featured && p.Status == PackageStatus.Published),
            DestinationsPublished: await db.Destinations.CountAsync(d => d.Status == DestinationStatus.Published, ct),
            DestinationsDraft: await db.Destinations.CountAsync(d => d.Status == DestinationStatus.Draft, ct),
            NeedingAttention: toFix.Count,
            ToFix: toFix.OrderBy(p => p.Title).Take(ListLength)
                .Select(p => new ListingToFix(p.Id, p.Title, p.ProductType.ToString(), p.Reason!)).ToList());
    }

    private record Row(
        Guid Id, EnquiryType Type, EnquiryStatus Status, Guid? PackageId, string? PackageTitle,
        ProductType? Product, long? QuotedMinor, string? Currency, DateTime CreatedAt, DateTime UpdatedAt,
        Guid? HandledById, string? Destination, string? HandledByName);
}
