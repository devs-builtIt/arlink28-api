using Arlink28.Api.Features.Catalogue.ResponseModels;

namespace Arlink28.Api.Features.AdminDestinations.ResponseModels;

public record DestinationStatusCounts(int All, int Draft, int Published);

public record AdminDestinationSummary(
    Guid Id,
    string Slug,
    string Name,
    string Country,
    string Kind,
    string Status,
    string? ParentName,
    string? HeroPath,
    int PlaceCount,
    int AttractionCount,
    DateTime UpdatedAt);

public record AdminDestinationListResponse(
    IReadOnlyList<AdminDestinationSummary> Items, int Total, int Page, int PageSize, DestinationStatusCounts Counts);

public record AdminDestinationDetail(
    Guid Id,
    string Slug,
    string Name,
    string Country,
    string Kind,
    string Status,
    Guid? ParentId,
    string? ParentName,
    string? Tagline,
    string? Summary,
    string? Description,
    string? BestTimeToVisit,
    string? HeroPath,
    string? HeroAlt,
    string? HeroCredit,
    double? Latitude,
    double? Longitude,
    int SortOrder,
    bool SlugLocked,
    DateTime? PublishedAt,
    DateTime UpdatedAt,
    IReadOnlyList<AdminDestinationSummary> Places,
    IReadOnlyList<AttractionResponse> Attractions,
    int PackageCount,
    /// <summary>What it still needs before it can be published. Empty means ready.</summary>
    IReadOnlyList<string> Missing);
