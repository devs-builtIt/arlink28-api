namespace Arlink28.Api.Features.Catalogue.ResponseModels;

/// <summary>
/// A destination as embedded in packages (the first four fields) and as a row in the public list,
/// which also fills the profile fields. Embedded copies leave them null.
/// </summary>
public record DestinationResponse(
    Guid Id,
    string Slug,
    string Name,
    string Country,
    string? Kind = null,
    string? ParentSlug = null,
    string? Tagline = null,
    string? Summary = null,
    string? HeroPath = null,
    string? HeroAlt = null,
    string? HeroCredit = null);

public record AttractionResponse(Guid Id, string Name, string? Summary, string? PhotoPath, string? PhotoAlt, string? PhotoCredit, int SortOrder);

public record DestinationDetailResponse(
    Guid Id,
    string Slug,
    string Name,
    string Country,
    string Kind,
    DestinationResponse? Parent,
    string? Tagline,
    string? Summary,
    string? Description,
    string? BestTimeToVisit,
    string? HeroPath,
    string? HeroAlt,
    string? HeroCredit,
    double? Latitude,
    double? Longitude,
    IReadOnlyList<DestinationResponse> Places,
    IReadOnlyList<AttractionResponse> Attractions,
    int PackageCount);
