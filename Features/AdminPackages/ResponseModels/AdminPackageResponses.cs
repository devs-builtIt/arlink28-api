using Arlink28.Api.Features.Catalogue.ResponseModels;
using Newtonsoft.Json.Linq;

namespace Arlink28.Api.Features.AdminPackages.ResponseModels;

public record AdminPackageSummary(
    Guid Id,
    string Slug,
    string Title,
    string Status,
    string Category,
    DestinationResponse Destination,
    int Nights,
    int Adults,
    int Children,
    string BaseCurrency,
    long? FromPriceMinor,
    string? HeroImagePath,
    int MediaCount,
    DateTime UpdatedAt,
    string ProductType,
    JObject? Details
);

/// <summary>How many packages sit in each status, for the tabs above the list. Ignores the status filter.</summary>
public record StatusCounts(int All, int Draft, int Published, int Archived);

public record AdminPackageListResponse(
    IReadOnlyList<AdminPackageSummary> Items,
    int Total,
    int Page,
    int PageSize,
    StatusCounts Counts
);

public record AdminStay(Guid Id, Guid PropertyId, string PropertyName, string DestinationName, int Nights, string? RoomType, int SortOrder);

/// <summary>A feature from the shared list, or a one-off line of the package's own (FeatureId null).</summary>
public record AdminFeature(Guid Id, string Section, Guid? FeatureId, string Label, string? Icon, string? Footnote, int SortOrder);

public record AdminRate(Guid Id, Guid SeasonId, string SeasonName, string Currency, long PriceMinor, long? ExtraNightPriceMinor);

public record AdminAddOn(Guid Id, string Name, string? Description, string Unit, string Currency, long PriceMinor, int SortOrder);

public record AdminPackageDetail(
    Guid Id,
    string Slug,
    string Status,
    string Title,
    string? Subtitle,
    string? Summary,
    string? Description,
    string Category,
    DestinationResponse Destination,
    int Nights,
    int MinNights,
    int Adults,
    int Children,
    string PricingBasis,
    string BaseCurrency,
    long? FromPriceMinor,
    bool Featured,
    string? SeoTitle,
    string? SeoDescription,
    DateTime? PublishedAt,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    IReadOnlyList<AdminStay> Stays,
    IReadOnlyList<AdminFeature> Features,
    IReadOnlyList<AdminRate> Rates,
    IReadOnlyList<AdminAddOn> AddOns,
    IReadOnlyList<MediaResponse> Media,
    string ProductType,
    /// <summary>The type-specific fields; null for holiday packages.</summary>
    JObject? Details
);

public record DateRangeResponse(DateOnly Start, DateOnly End);
public record PropertyOption(Guid Id, string Slug, string Name, Guid DestinationId, string DestinationName);
public record SeasonOption(Guid Id, string Slug, string Name, IReadOnlyList<DateRangeResponse> Ranges);
public record FeatureOption(Guid Id, string Slug, string Label, string? Icon);

/// <summary>The lists the package form picks from.</summary>
public record AdminReferenceResponse(
    IReadOnlyList<DestinationResponse> Destinations,
    IReadOnlyList<PropertyOption> Properties,
    IReadOnlyList<SeasonOption> Seasons,
    IReadOnlyList<FeatureOption> Features
);
