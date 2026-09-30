using Arlink28.Api.Data;
using Arlink28.Api.Data.Entities;
using Arlink28.Api.Features.Catalogue.RequestModels;
using Arlink28.Api.Features.Catalogue.ResponseModels;
using Arlink28.Api.Features.Catalogue.Services.Interfaces;
using Arlink28.Api.Features.Shared.Interfaces;
using Arlink28.Api.Helpers;
using Microsoft.EntityFrameworkCore;

namespace Arlink28.Api.Features.Catalogue.Services;

public class CatalogueService(ApplicationDbContext db) : ICatalogueService, IScoped
{
    public async Task<PackageListResponse> ListPackagesAsync(PackageListRequest request, CancellationToken ct = default)
    {
        var query = db.Packages
            .AsNoTracking()
            .Where(p => p.Status == PackageStatus.Published)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.Destination))
            query = query.Where(p => p.Destination.Slug == request.Destination);

        if (!string.IsNullOrWhiteSpace(request.Partner))
            query = query.Where(p => p.Stays.Any(s => s.Property.Partner != null && s.Property.Partner.Slug == request.Partner));

        if (!string.IsNullOrWhiteSpace(request.Category))
            query = query.Where(p => p.Category == request.Category);

        if (request.Adults.HasValue)
            query = query.Where(p => p.Adults >= request.Adults.Value);

        if (request.Children.HasValue)
            query = query.Where(p => p.Children >= request.Children.Value);

        if (request.Featured.HasValue)
            query = query.Where(p => p.Featured == request.Featured.Value);

        if (!string.IsNullOrWhiteSpace(request.Q))
        {
            var term = request.Q.Trim().ToLower();
            query = query.Where(p => p.Title.ToLower().Contains(term)
                || (p.Subtitle != null && p.Subtitle.ToLower().Contains(term))
                || (p.Summary != null && p.Summary.ToLower().Contains(term)));
        }

        var limit = Math.Clamp(request.Limit, 1, 100);

        // A page number asks for numbered pages with a total, and a choice of order.
        if (request.Page is { } requestedPage)
        {
            var page = Math.Max(1, requestedPage);
            var total = await query.CountAsync(ct);
            var ordered = (request.Sort?.ToLowerInvariant()) switch
            {
                "price" => query.OrderBy(p => p.FromPriceMinor == null).ThenBy(p => p.FromPriceMinor),
                "-price" => query.OrderBy(p => p.FromPriceMinor == null).ThenByDescending(p => p.FromPriceMinor),
                "nights" => query.OrderBy(p => p.Nights).ThenByDescending(p => p.Featured),
                _ => query.OrderByDescending(p => p.Featured).ThenBy(p => p.SortOrder),
            };
            var pageItems = await WithCardData(ordered.ThenBy(p => p.Id).Skip((page - 1) * limit).Take(limit)).ToListAsync(ct);
            return new PackageListResponse(pageItems.Select(MapToCard).ToList(), null, total, page, limit);
        }

        // Cursor-based pagination: featured DESC, sortOrder ASC, id ASC
        if (!string.IsNullOrWhiteSpace(request.Cursor))
        {
            var cursor = DecodeCursor(request.Cursor);
            if (cursor is not null)
            {
                query = query.Where(p =>
                    !p.Featured && !cursor.Featured ||
                    p.SortOrder > cursor.SortOrder ||
                    (p.SortOrder == cursor.SortOrder && p.Id.CompareTo(cursor.Id) > 0));
            }
        }

        var items = await WithCardData(query
                .OrderByDescending(p => p.Featured)
                .ThenBy(p => p.SortOrder)
                .ThenBy(p => p.Id)
                .Take(limit + 1))
            .ToListAsync(ct);

        string? nextCursor = null;
        if (items.Count > limit)
        {
            items = items.Take(limit).ToList();
            var last = items[^1];
            nextCursor = EncodeCursor(last.Featured, last.SortOrder, last.Id);
        }

        return new PackageListResponse(items.Select(MapToCard).ToList(), nextCursor);
    }

    /// <summary>What a card shows: the destination, the hero photo, a few inclusions and the lodges.</summary>
    private static IQueryable<Package> WithCardData(IQueryable<Package> packages) => packages
        .Include(p => p.Destination)
        .Include(p => p.Media)
        .Include(p => p.Features).ThenInclude(f => f.Feature)
        .Include(p => p.Stays).ThenInclude(s => s.Property)
        .AsSplitQuery();

    public async Task<PackageDetailResponse?> GetPackageAsync(string slug, CancellationToken ct = default)
    {
        var package = await db.Packages
            .AsNoTracking()
            .Where(p => p.Slug == slug && p.Status == PackageStatus.Published)
            .Include(p => p.Destination)
            .Include(p => p.Stays).ThenInclude(s => s.Property).ThenInclude(pr => pr.Destination)
            .Include(p => p.Features).ThenInclude(f => f.Feature)
            .Include(p => p.AddOns)
            .Include(p => p.Media)
            .Include(p => p.Rates).ThenInclude(r => r.Season).ThenInclude(s => s.Ranges)
            .AsSplitQuery()
            .FirstOrDefaultAsync(ct);

        return package is null ? null : MapToDetail(package);
    }

    public async Task<QuoteResponse> QuotePackageAsync(string slug, QuoteRequest request, CancellationToken ct = default)
    {
        var package = await db.Packages
            .AsNoTracking()
            .Where(p => p.Slug == slug && p.Status == PackageStatus.Published)
            .Include(p => p.AddOns)
            .Include(p => p.Rates).ThenInclude(r => r.Season).ThenInclude(s => s.Ranges)
            .AsSplitQuery()
            .FirstOrDefaultAsync(ct)
            ?? throw new AppException($"Package '{slug}' not found.");

        var addOns = ParseAddOns(request.AddOns);

        var engineRequest = new PricingRequest(
            request.CheckIn,
            request.Nights,
            request.Currency,
            addOns
        );

        var result = PricingEngine.Quote(package, engineRequest);

        return new QuoteResponse(
            result.TotalMinor,
            result.Currency,
            result.BaseMinor,
            result.Nights,
            result.Lines.Select(l => new QuoteLineResponse(l.Label, l.AmountMinor, l.Currency)).ToList()
        );
    }

    public async Task<IReadOnlyList<DestinationResponse>> ListDestinationsAsync(CancellationToken ct = default)
    {
        return await db.Destinations
            .AsNoTracking()
            .OrderBy(d => d.Name)
            .Select(d => new DestinationResponse(d.Id, d.Slug, d.Name, d.Country))
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<PartnerResponse>> ListPartnersAsync(CancellationToken ct = default)
    {
        return await db.Partners
            .AsNoTracking()
            .OrderBy(p => p.Name)
            .Select(p => new PartnerResponse(p.Id, p.Slug, p.Name, p.Tagline, p.LogoPath))
            .ToListAsync(ct);
    }

    private static PackageCardResponse MapToCard(Package p) => new(
        p.Id, p.Slug, p.Title, p.Subtitle, p.Summary,
        p.Category,
        new DestinationResponse(p.Destination.Id, p.Destination.Slug, p.Destination.Name, p.Destination.Country),
        p.Nights, p.Adults, p.Children,
        p.BaseCurrency, p.FromPriceMinor, p.Featured,
        p.Media.Where(m => m.Role == MediaRole.Hero).OrderBy(m => m.SortKey).Select(m => m.Path).FirstOrDefault(),
        CardHighlights(p),
        p.Stays.OrderBy(s => s.SortOrder).Select(s => s.Property.Name).Distinct().Take(3).ToList()
    );

    private static IReadOnlyList<string> CardHighlights(Package p) => p.Features
        .Where(f => f.Section is FeatureSection.Highlight or FeatureSection.Included)
        .OrderBy(f => f.Section == FeatureSection.Highlight ? 0 : 1).ThenBy(f => f.SortOrder)
        .Select(f => f.LabelOverride ?? f.Feature?.Label ?? string.Empty)
        .Where(label => label.Length > 0)
        .Take(3)
        .ToList();

    private static PackageDetailResponse MapToDetail(Package p) => new(
        p.Id, p.Slug, p.Title, p.Subtitle, p.Summary, p.Description,
        p.Category,
        new DestinationResponse(p.Destination.Id, p.Destination.Slug, p.Destination.Name, p.Destination.Country),
        p.Nights, p.MinNights, p.Adults, p.Children,
        p.PricingBasis.ToString(),
        p.BaseCurrency, p.FromPriceMinor, p.Featured,
        p.SeoTitle, p.SeoDescription,
        p.Stays.OrderBy(s => s.SortOrder).Select(s => new StayResponse(
            s.Id, s.Nights, s.RoomType, s.SortOrder,
            s.Property.Name, s.Property.Slug, s.Property.Destination.Name)).ToList(),
        p.Features.OrderBy(f => f.SortOrder).Select(f => new FeatureResponse(
            f.Section.ToString(),
            f.LabelOverride ?? f.Feature?.Label ?? string.Empty,
            f.Feature?.Icon,
            f.Footnote,
            f.SortOrder)).ToList(),
        p.AddOns.OrderBy(a => a.SortOrder).Select(a => new AddOnResponse(
            a.Id, a.Name, a.Description, a.Unit.ToString(), a.Currency, a.PriceMinor, a.SortOrder)).ToList(),
        p.Media.OrderBy(m => m.SortKey).Select(m => new MediaResponse(
            m.Id, m.Role.ToString(), m.Path, m.Alt, m.Caption, m.Width, m.Height,
            m.VideoProvider?.ToString(), m.VideoId, m.SortKey)).ToList(),
        p.Rates.Select(r => new SeasonRateResponse(
            r.Season.Name, r.Season.Slug, r.Currency, r.PriceMinor, r.ExtraNightPriceMinor,
            r.Season.Ranges.OrderBy(x => x.StartDate).Select(x => new SeasonRangeResponse(x.StartDate, x.EndDate)).ToList())).ToList()
    );

    private static IReadOnlyList<(Guid, int)> ParseAddOns(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return [];

        var result = new List<(Guid, int)>();
        foreach (var part in raw.Split(',', StringSplitOptions.RemoveEmptyEntries))
        {
            var segments = part.Trim().Split(':');
            if (segments.Length == 2 && Guid.TryParse(segments[0], out var id) && int.TryParse(segments[1], out var qty))
                result.Add((id, qty));
        }
        return result;
    }

    private record CursorData(bool Featured, int SortOrder, Guid Id);

    private static string EncodeCursor(bool featured, int sortOrder, Guid id)
    {
        var raw = $"{(featured ? 1 : 0)}:{sortOrder}:{id}";
        return Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(raw));
    }

    private static CursorData? DecodeCursor(string cursor)
    {
        try
        {
            var raw = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(cursor));
            var parts = raw.Split(':');
            if (parts.Length == 3 && int.TryParse(parts[1], out var sortOrder) && Guid.TryParse(parts[2], out var id))
                return new CursorData(parts[0] == "1", sortOrder, id);
        }
        catch { /* malformed cursor — ignore */ }
        return null;
    }
}
