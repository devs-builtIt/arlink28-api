namespace Arlink28.Api.Features.Catalogue.ResponseModels;

/// <summary>Total, Page and PageSize are set only when the request asked for a Page.</summary>
public record PackageListResponse(
    IReadOnlyList<PackageCardResponse> Items,
    string? NextCursor,
    int? Total = null,
    int? Page = null,
    int? PageSize = null
);
