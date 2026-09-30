using Arlink28.Api.Data.Entities;

namespace Arlink28.Api.Features.Catalogue.ResponseModels;

public record StayResponse(
    Guid Id,
    int Nights,
    string? RoomType,
    int SortOrder,
    string PropertyName,
    string PropertySlug,
    string DestinationName
);

public record FeatureResponse(
    string Section,
    string Label,
    string? Icon,
    string? Footnote,
    int SortOrder
);

public record AddOnResponse(
    Guid Id,
    string Name,
    string? Description,
    string Unit,
    string Currency,
    long PriceMinor,
    int SortOrder
);

public record MediaResponse(
    Guid Id,
    string Role,
    string Path,
    string? Alt,
    string? Caption,
    int? Width,
    int? Height,
    string? VideoProvider,
    string? VideoId,
    int SortKey
);

public record SeasonRangeResponse(DateOnly Start, DateOnly End);

public record SeasonRateResponse(
    string SeasonName,
    string SeasonSlug,
    string Currency,
    long PriceMinor,
    long? ExtraNightPriceMinor,
    /// <summary>The check-in dates this rate covers.</summary>
    IReadOnlyList<SeasonRangeResponse> Ranges
);

public record PackageDetailResponse(
    Guid Id,
    string Slug,
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
    IReadOnlyList<StayResponse> Stays,
    IReadOnlyList<FeatureResponse> Features,
    IReadOnlyList<AddOnResponse> AddOns,
    IReadOnlyList<MediaResponse> Media,
    IReadOnlyList<SeasonRateResponse> Rates
);
