using Arlink28.Api.Data;
using Arlink28.Api.Data.Entities;
using Arlink28.Api.Features.Catalogue.RequestModels;
using Arlink28.Api.Features.Catalogue.Services.Interfaces;
using Arlink28.Api.Features.Enquiries.RequestModels;
using Arlink28.Api.Features.Enquiries.ResponseModels;
using Arlink28.Api.Features.Enquiries.Services.Interfaces;
using Arlink28.Api.Features.Shared.Interfaces;
using Arlink28.Api.Helpers;
using Microsoft.EntityFrameworkCore;

namespace Arlink28.Api.Features.Enquiries.Services;

public class EnquiryService(
    ApplicationDbContext db,
    ICatalogueService catalogue,
    IEnquiryNotificationQueue notifications) : IEnquiryService, IScoped
{
    /// <summary>An identical enquiry inside this window is the same one, sent twice.</summary>
    private static readonly TimeSpan DuplicateWindow = TimeSpan.FromMinutes(10);
    private const int ReferenceAttempts = 3;

    public async Task<CreateEnquiryResponse> CreateAsync(CreateEnquiryRequest request, CancellationToken ct = default)
    {
        // A bot filled the hidden field. It gets the same answer as a guest and nothing is saved.
        if (!string.IsNullOrWhiteSpace(request.Website))
            return new CreateEnquiryResponse(
                $"ENQ-{DateTime.UtcNow.Year}-{Random.Shared.Next(1, 10_000):0000}");

        var now = DateTime.UtcNow;
        var email = request.Email.Trim();
        var enquiry = new Enquiry
        {
            Type = request.Type,
            Status = EnquiryStatus.New,
            Name = request.Name.Trim(),
            Email = email,
            Phone = Clean(request.Phone),
            Subject = Clean(request.Subject),
            Message = Clean(request.Message),
            SourceUrl = Clean(request.SourceUrl),
            ConsentAt = now,
            CreatedAt = now,
            UpdatedAt = now,
        };

        if (request.Type == EnquiryType.Package)
            await AttachPackageAsync(enquiry, request, ct);

        var since = now - DuplicateWindow;
        var lowered = email.ToLower();
        var duplicate = await db.Enquiries.AsNoTracking()
            .Where(e => e.CreatedAt >= since && e.Type == enquiry.Type && e.PackageId == enquiry.PackageId
                        && e.CheckIn == enquiry.CheckIn && e.Email.ToLower() == lowered)
            .OrderByDescending(e => e.CreatedAt)
            .Select(e => e.Reference)
            .FirstOrDefaultAsync(ct);
        if (duplicate is not null) return new CreateEnquiryResponse(duplicate);

        for (var attempt = 1; ; attempt++)
        {
            enquiry.Reference = await NextReferenceAsync(now.Year, ct);
            enquiry.Id = Guid.NewGuid();
            db.Enquiries.Add(enquiry);
            db.AuditLogs.Add(new AuditLog
            {
                Action = "Enquiry.Created",
                EntityType = "Enquiry",
                EntityId = enquiry.Id.ToString(),
                CreatedAt = now,
            });
            try
            {
                await db.SaveChangesAsync(ct);
                // The email is sent by a background worker; the guest doesn't wait for it.
                notifications.Enqueue(enquiry.Id);
                return new CreateEnquiryResponse(enquiry.Reference);
            }
            catch (DbUpdateException) when (attempt < ReferenceAttempts)
            {
                // Two guests took the same next number at once; the unique index refused the second.
                db.ChangeTracker.Clear();
            }
        }
    }

    public async Task<EnquiryListResponse> ListAsync(string? status, int page, int pageSize, CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var byStatus = await db.Enquiries.AsNoTracking()
            .GroupBy(e => e.Status).Select(g => new { g.Key, Count = g.Count() }).ToListAsync(ct);
        int CountOf(EnquiryStatus s) => byStatus.FirstOrDefault(x => x.Key == s)?.Count ?? 0;
        var counts = new EnquiryStatusCounts(byStatus.Sum(x => x.Count), CountOf(EnquiryStatus.New),
            CountOf(EnquiryStatus.Contacted), CountOf(EnquiryStatus.Closed));

        var filtered = db.Enquiries.AsNoTracking().AsQueryable();
        if (Enum.TryParse<EnquiryStatus>(status, ignoreCase: true, out var parsed))
            filtered = filtered.Where(e => e.Status == parsed);
        var total = await filtered.CountAsync(ct);

        var rows = await filtered
            .OrderByDescending(e => e.CreatedAt).ThenBy(e => e.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .ToListAsync(ct);

        return new EnquiryListResponse(rows.Select(ToItem).ToList(), total, page, pageSize, counts);
    }

    public async Task<EnquiryDetail?> GetAsync(Guid id, CancellationToken ct = default)
    {
        var enquiry = await db.Enquiries.AsNoTracking().FirstOrDefaultAsync(e => e.Id == id, ct);
        return enquiry is null ? null : ToDetail(enquiry);
    }

    public async Task<EnquiryDetail?> SetStatusAsync(
        Guid id, UpdateEnquiryRequest request, Guid actorId, CancellationToken ct = default)
    {
        var enquiry = await db.Enquiries.FirstOrDefaultAsync(e => e.Id == id, ct);
        if (enquiry is null) return null;
        if (enquiry.Status == request.Status) return ToDetail(enquiry);

        var before = enquiry.Status;
        enquiry.Status = request.Status;
        enquiry.HandledById = request.Status == EnquiryStatus.New ? null : actorId;
        enquiry.UpdatedAt = DateTime.UtcNow;
        db.AuditLogs.Add(new AuditLog
        {
            Action = "Enquiry.StatusChanged",
            EntityType = "Enquiry",
            EntityId = enquiry.Id.ToString(),
            ActorId = actorId.ToString(),
            Before = before.ToString(),
            After = request.Status.ToString(),
            CreatedAt = enquiry.UpdatedAt,
        });
        await db.SaveChangesAsync(ct);
        return ToDetail(enquiry);
    }

    /// <summary>
    /// Links the published package and stores the API's own quote. A date with no rate still saves,
    /// without a total; any other quote problem (past date, too few nights) is for the guest to fix.
    /// </summary>
    private async Task AttachPackageAsync(Enquiry enquiry, CreateEnquiryRequest request, CancellationToken ct)
    {
        var slug = request.Slug!.Trim();
        var package = await db.Packages.AsNoTracking()
            .Where(p => p.Slug == slug && p.Status == PackageStatus.Published)
            .Select(p => new { p.Id, p.Title, p.BaseCurrency })
            .FirstOrDefaultAsync(ct)
            ?? throw new AppException($"Package '{slug}' is not available for enquiries.");

        enquiry.PackageId = package.Id;
        enquiry.PackageTitle = package.Title;
        enquiry.CheckIn = request.CheckIn;
        enquiry.Nights = request.Nights;

        if (request.CheckIn is not { } checkIn) return;
        try
        {
            var quote = await catalogue.QuotePackageAsync(
                slug, new QuoteRequest(checkIn, request.Nights, package.BaseCurrency), ct);
            enquiry.Nights = quote.Nights;
            enquiry.QuotedTotalMinor = quote.TotalMinor;
            enquiry.Currency = quote.Currency;
        }
        catch (QuoteException ex) when (ex.Code is QuoteError.NoRateForDate or QuoteError.CurrencyNotAvailable)
        {
            // Saved without a total; staff price it by hand.
        }
    }

    /// <summary>ENQ-2026-0042: the next number for the year. The unique index settles a race.</summary>
    private async Task<string> NextReferenceAsync(int year, CancellationToken ct)
    {
        var prefix = $"ENQ-{year}-";
        var last = await db.Enquiries.AsNoTracking()
            .Where(e => e.Reference.StartsWith(prefix))
            .OrderByDescending(e => e.Reference)
            .Select(e => e.Reference)
            .FirstOrDefaultAsync(ct);
        var next = last is not null && int.TryParse(last[prefix.Length..], out var n) ? n + 1 : 1;
        return $"{prefix}{next:0000}";
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static EnquiryListItem ToItem(Enquiry e) => new(
        e.Id, e.Reference, e.Type, e.Status, e.PackageTitle, e.Subject, e.CheckIn, e.Nights, e.QuotedTotalMinor,
        e.Currency, e.Name, e.Email, e.CreatedAt);

    private static EnquiryDetail ToDetail(Enquiry e) => new(
        e.Id, e.Reference, e.Type, e.Status, e.PackageId, e.PackageTitle, e.CheckIn, e.Nights,
        e.QuotedTotalMinor, e.Currency, e.Name, e.Email, e.Phone, e.Subject, e.Message, e.ConsentAt,
        e.SourceUrl, e.HandledById, e.CreatedAt, e.UpdatedAt);
}
