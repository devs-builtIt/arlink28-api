using Arlink28.Api.Data;
using Arlink28.Api.Data.Entities;
using Arlink28.Api.Features.AdminPackages.RequestModels;
using Arlink28.Api.Features.AdminPackages.ResponseModels;
using Arlink28.Api.Features.AdminPackages.Services.Interfaces;
using Arlink28.Api.Features.Catalogue.ResponseModels;
using Arlink28.Api.Features.Shared.Interfaces;
using Arlink28.Api.Helpers;
using Microsoft.EntityFrameworkCore;
using static Arlink28.Api.Features.AdminPackages.Services.AdminPackageMapper;

namespace Arlink28.Api.Features.AdminPackages.Services;

public class AdminPackageContentService(ApplicationDbContext db) : IAdminPackageContentService, IScoped
{
    private static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow);

    public async Task<AdminReferenceResponse> ReferenceAsync(CancellationToken ct = default) => new(
        await db.Destinations.AsNoTracking().OrderBy(d => d.Name).Select(d => new DestinationResponse(d.Id, d.Slug, d.Name, d.Country)).ToListAsync(ct),
        await db.Properties.AsNoTracking().OrderBy(p => p.Name)
            .Select(p => new PropertyOption(p.Id, p.Slug, p.Name, p.DestinationId, p.Destination.Name)).ToListAsync(ct),
        (await db.Seasons.AsNoTracking().Include(s => s.Ranges).OrderBy(s => s.Name).ToListAsync(ct))
            .Select(s => new SeasonOption(s.Id, s.Slug, s.Name,
                s.Ranges.OrderBy(r => r.StartDate).Select(r => new DateRangeResponse(r.StartDate, r.EndDate)).ToList())).ToList(),
        await db.Features.AsNoTracking().OrderBy(f => f.Label).Select(f => new FeatureOption(f.Id, f.Slug, f.Label, f.Icon)).ToListAsync(ct));

    public async Task<AdminPackageDetail?> ReplaceStaysAsync(Guid id, IReadOnlyList<StayInput> stays, CancellationToken ct = default)
    {
        var package = await LoadAsync(id, ct);
        if (package is null) return null;
        if (stays.Count > 20) throw new AppException("A package can have at most 20 stays.");

        var propertyIds = stays.Select(s => s.PropertyId).Distinct().ToList();
        var known = await db.Properties.Where(p => propertyIds.Contains(p.Id)).Select(p => p.Id).ToListAsync(ct);
        foreach (var (stay, n) in stays.Select((s, i) => (s, i + 1)))
        {
            if (!known.Contains(stay.PropertyId)) throw new AppException($"Stay {n}: choose a property.");
            if (stay.Nights is < 1 or > 60) throw new AppException($"Stay {n}: nights must be between 1 and 60.");
            if (stay.RoomType?.Length > 100) throw new AppException($"Stay {n}: the room type is too long.");
        }

        db.PackageStays.RemoveRange(package.Stays);
        package.Stays.Clear();
        foreach (var (stay, i) in stays.Select((s, i) => (s, i)))
            db.PackageStays.Add(new PackageStay
            {
                PackageId = id, PropertyId = stay.PropertyId, Nights = stay.Nights,
                RoomType = Clean(stay.RoomType), SortOrder = i,
            });
        return await SaveAsync(package, ct);
    }

    public async Task<AdminPackageDetail?> ReplaceFeaturesAsync(Guid id, IReadOnlyList<FeatureInput> features, CancellationToken ct = default)
    {
        var package = await LoadAsync(id, ct);
        if (package is null) return null;
        if (features.Count > 150) throw new AppException("That is too many lines. Keep it to 150 or fewer.");

        var featureIds = features.Where(f => f.FeatureId.HasValue).Select(f => f.FeatureId!.Value).Distinct().ToList();
        var known = await db.Features.Where(f => featureIds.Contains(f.Id)).Select(f => f.Id).ToListAsync(ct);
        foreach (var (f, n) in features.Select((f, i) => (f, i + 1)))
        {
            if (!Enum.IsDefined(f.Section)) throw new AppException($"Line {n}: choose a section.");
            if (f.FeatureId is { } fid && !known.Contains(fid)) throw new AppException($"Line {n}: that feature does not exist.");
            if (f.FeatureId is null && string.IsNullOrWhiteSpace(f.Label)) throw new AppException($"Line {n}: write the line, or choose one from the list.");
            if (f.Label?.Length > 200 || f.Footnote?.Length > 300) throw new AppException($"Line {n}: the text is too long.");
        }

        db.PackageFeatures.RemoveRange(package.Features);
        package.Features.Clear();
        foreach (var (f, i) in features.Select((f, i) => (f, i)))
            db.PackageFeatures.Add(new PackageFeature
            {
                PackageId = id, Section = f.Section, FeatureId = f.FeatureId,
                // A shared feature supplies its own label; only a one-off line stores text.
                LabelOverride = f.FeatureId is null ? Clean(f.Label) : null,
                Footnote = Clean(f.Footnote), SortOrder = i,
            });
        return await SaveAsync(package, ct);
    }

    public async Task<AdminPackageDetail?> ReplaceRatesAsync(Guid id, IReadOnlyList<RateInput> rates, CancellationToken ct = default)
    {
        var package = await LoadAsync(id, ct);
        if (package is null) return null;
        if (rates.Count > 60) throw new AppException("A package can have at most 60 rates.");

        var seasonIds = rates.Select(r => r.SeasonId).Distinct().ToList();
        var seasons = await db.Seasons.Include(s => s.Ranges).Where(s => seasonIds.Contains(s.Id)).ToDictionaryAsync(s => s.Id, ct);
        var normalised = new List<RateInput>();
        foreach (var (r, n) in rates.Select((r, i) => (r, i + 1)))
        {
            if (!seasons.ContainsKey(r.SeasonId)) throw new AppException($"Rate {n}: choose a season.");
            var currency = (r.Currency ?? "").Trim().ToUpperInvariant();
            if (currency.Length != 3) throw new AppException($"Rate {n}: the currency needs three letters, like USD.");
            if (r.PriceMinor <= 0) throw new AppException($"Rate {n}: enter a price above zero.");
            if (r.ExtraNightPriceMinor is < 0) throw new AppException($"Rate {n}: the extra night price cannot be negative.");
            normalised.Add(r with { Currency = currency });
        }

        var seen = new HashSet<(Guid, string)>();
        foreach (var r in normalised)
            if (!seen.Add((r.SeasonId, r.Currency)))
                throw new AppException($"{seasons[r.SeasonId].Name} is listed twice in {r.Currency}.");

        // A check-in date must match one rate, so two seasons in the same currency cannot share dates.
        foreach (var group in normalised.GroupBy(r => r.Currency))
        {
            var list = group.Select(r => seasons[r.SeasonId]).ToList();
            for (var a = 0; a < list.Count; a++)
                for (var b = a + 1; b < list.Count; b++)
                    if (list[a].Ranges.Any(x => list[b].Ranges.Any(y => x.StartDate <= y.EndDate && y.StartDate <= x.EndDate)))
                        throw new AppException($"{list[a].Name} and {list[b].Name} overlap, so a date could match two {group.Key} prices.");
        }

        db.PackageRates.RemoveRange(package.Rates);
        package.Rates.Clear();
        foreach (var r in normalised)
            db.PackageRates.Add(new PackageRate
            {
                PackageId = id, SeasonId = r.SeasonId, Currency = r.Currency,
                PriceMinor = r.PriceMinor, ExtraNightPriceMinor = r.ExtraNightPriceMinor,
            });
        return await SaveAsync(package, ct);
    }

    public async Task<AdminPackageDetail?> ReplaceAddOnsAsync(Guid id, IReadOnlyList<AddOnInput> addOns, CancellationToken ct = default)
    {
        var package = await LoadAsync(id, ct);
        if (package is null) return null;
        if (addOns.Count > 40) throw new AppException("A package can have at most 40 add-ons.");

        foreach (var (a, n) in addOns.Select((a, i) => (a, i + 1)))
        {
            if (string.IsNullOrWhiteSpace(a.Name)) throw new AppException($"Add-on {n}: give it a name.");
            if (a.Name.Length > 150 || a.Description?.Length > 500) throw new AppException($"Add-on {n}: the text is too long.");
            if (!Enum.IsDefined(a.Unit)) throw new AppException($"Add-on {n}: choose what it is charged per.");
            if ((a.Currency ?? "").Trim().Length != 3) throw new AppException($"Add-on {n}: the currency needs three letters, like USD.");
            if (a.PriceMinor < 0) throw new AppException($"Add-on {n}: the price cannot be negative.");
            if (a.Id is { } aid && package.AddOns.All(x => x.Id != aid)) throw new AppException($"Add-on {n}: it does not belong to this package.");
        }
        if (addOns.Where(a => a.Id.HasValue).GroupBy(a => a.Id).Any(g => g.Count() > 1))
            throw new AppException("An add-on is listed twice.");

        // Keep the rows that are still there, so a quote that names an add-on keeps working.
        var keep = addOns.Where(a => a.Id.HasValue).Select(a => a.Id!.Value).ToHashSet();
        foreach (var gone in package.AddOns.Where(a => !keep.Contains(a.Id)).ToList())
        {
            db.PackageAddOns.Remove(gone);
            package.AddOns.Remove(gone);
        }
        foreach (var (a, i) in addOns.Select((a, i) => (a, i)))
        {
            var row = a.Id is { } aid ? package.AddOns.First(x => x.Id == aid) : new PackageAddOn { PackageId = id };
            row.Name = a.Name.Trim();
            row.Description = Clean(a.Description);
            row.Unit = a.Unit;
            row.Currency = a.Currency.Trim().ToUpperInvariant();
            row.PriceMinor = a.PriceMinor;
            row.SortOrder = i;
            if (a.Id is null) db.PackageAddOns.Add(row);
        }
        return await SaveAsync(package, ct);
    }

    public IReadOnlyList<string> PublishChecklist(Package p, DateOnly today)
    {
        var missing = new List<string>();
        if (string.IsNullOrWhiteSpace(p.Title)) missing.Add("A name");
        if (string.IsNullOrWhiteSpace(p.Summary)) missing.Add("A summary");
        if (p.MinNights < 1 || p.Nights < p.MinNights) missing.Add("Nights that are at least the minimum stay, and at least one");
        if (p.Adults < 1) missing.Add("At least one adult");
        if (p.Stays.Count == 0) missing.Add("At least one stay");
        else if (p.Stays.Sum(s => s.Nights) != p.Nights)
            missing.Add($"Stays that add up to {p.Nights} {(p.Nights == 1 ? "night" : "nights")} (they add up to {p.Stays.Sum(s => s.Nights)})");
        if (!p.Rates.Any(r => r.Currency == p.BaseCurrency && r.Season.Ranges.Any(x => x.EndDate >= today)))
            missing.Add($"A {p.BaseCurrency} rate for a season that has not ended");
        if (p.Media.Count(m => m.Role == MediaRole.Hero) != 1) missing.Add("A main photo");
        return missing;
    }

    public async Task<PublishOutcome> PublishAsync(Guid id, CancellationToken ct = default)
    {
        var package = await LoadAsync(id, ct);
        if (package is null) return new PublishOutcome(null, []);
        var missing = PublishChecklist(package, Today);
        if (missing.Count > 0) return new PublishOutcome(ToDetail(package), missing);

        package.Status = PackageStatus.Published;
        package.PublishedAt ??= DateTime.UtcNow;
        return new PublishOutcome(await SaveAsync(package, ct), []);
    }

    public async Task<AdminPackageDetail?> UnpublishAsync(Guid id, CancellationToken ct = default)
    {
        var package = await LoadAsync(id, ct);
        if (package is null) return null;
        package.Status = PackageStatus.Draft;
        return await SaveAsync(package, ct);
    }

    public async Task<AdminPackageDetail?> ArchiveAsync(Guid id, CancellationToken ct = default)
    {
        var package = await LoadAsync(id, ct);
        if (package is null) return null;
        package.Status = PackageStatus.Archived;
        return await SaveAsync(package, ct);
    }

    private Task<Package?> LoadAsync(Guid id, CancellationToken ct) => db.Packages.WithContent().FirstOrDefaultAsync(p => p.Id == id, ct);

    private async Task<AdminPackageDetail> SaveAsync(Package package, CancellationToken ct)
    {
        package.UpdatedAt = DateTime.UtcNow;
        package.Version++;
        await db.SaveChangesAsync(ct);

        // Reload, so ordering, joins and the recomputed from-price are what a fresh GET returns.
        db.ChangeTracker.Clear();
        var fresh = await db.Packages.WithContent().FirstAsync(p => p.Id == package.Id, ct);
        fresh.FromPriceMinor = FromPrice(fresh, Today);
        await db.SaveChangesAsync(ct);
        return ToDetail(fresh);
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
