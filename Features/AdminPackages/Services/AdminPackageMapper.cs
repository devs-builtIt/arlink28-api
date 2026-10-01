using Arlink28.Api.Data.Entities;
using Arlink28.Api.Features.AdminPackages.ResponseModels;
using Arlink28.Api.Features.Catalogue.ResponseModels;
using Microsoft.EntityFrameworkCore;

namespace Arlink28.Api.Features.AdminPackages.Services;

/// <summary>Loads a package with everything the admin form shows, and turns it into the response.</summary>
public static class AdminPackageMapper
{
    public static IQueryable<Package> WithContent(this IQueryable<Package> packages) => packages
        .Include(p => p.Destination)
        .Include(p => p.Media)
        .Include(p => p.Stays).ThenInclude(s => s.Property).ThenInclude(pr => pr.Destination)
        .Include(p => p.Features).ThenInclude(f => f.Feature)
        .Include(p => p.Rates).ThenInclude(r => r.Season).ThenInclude(s => s.Ranges)
        .Include(p => p.AddOns)
        .AsSplitQuery();

    public static DestinationResponse ToResponse(Destination d) => new(d.Id, d.Slug, d.Name, d.Country);

    public static MediaResponse ToResponse(PackageMedia m) => new(
        m.Id, m.Role.ToString(), m.Path, m.Alt, m.Caption, m.Width, m.Height,
        m.VideoProvider?.ToString(), m.VideoId, m.SortKey);

    public static AdminPackageDetail ToDetail(Package p) => new(
        p.Id, p.Slug, p.Status.ToString(), p.Title, p.Subtitle, p.Summary, p.Description, p.Category,
        ToResponse(p.Destination), p.Nights, p.MinNights, p.Adults, p.Children,
        p.PricingBasis.ToString(), p.BaseCurrency, p.FromPriceMinor, p.Featured,
        p.SeoTitle, p.SeoDescription, p.PublishedAt, p.CreatedAt, p.UpdatedAt,
        p.Stays.OrderBy(s => s.SortOrder).Select(s => new AdminStay(
            s.Id, s.PropertyId, s.Property.Name, s.Property.Destination.Name, s.Nights, s.RoomType, s.SortOrder)).ToList(),
        p.Features.OrderBy(f => f.SortOrder).Select(f => new AdminFeature(
            f.Id, f.Section.ToString(), f.FeatureId, f.LabelOverride ?? f.Feature?.Label ?? string.Empty,
            f.Feature?.Icon, f.Footnote, f.SortOrder)).ToList(),
        p.Rates.OrderBy(r => r.Season.Name).ThenBy(r => r.Currency).Select(r => new AdminRate(
            r.Id, r.SeasonId, r.Season.Name, r.Currency, r.PriceMinor, r.ExtraNightPriceMinor)).ToList(),
        p.AddOns.OrderBy(a => a.SortOrder).Select(a => new AdminAddOn(
            a.Id, a.Name, a.Description, a.Unit.ToString(), a.Currency, a.PriceMinor, a.SortOrder)).ToList(),
        p.Media.OrderBy(m => m.SortKey).Select(ToResponse).ToList(),
        p.ProductType.ToString(), ProductDetails.ToJson(p.Details));

    /// <summary>
    /// The lowest base-currency rate among seasons that still have a check-in date on or after
    /// <paramref name="today"/>; null when there is none. Same rule as the catalogue seed.
    /// </summary>
    public static long? FromPrice(Package p, DateOnly today) => p.Rates
        .Where(r => r.Currency == p.BaseCurrency && r.Season.Ranges.Any(x => x.EndDate >= today))
        .Select(r => (long?)r.PriceMinor)
        .Min();
}
