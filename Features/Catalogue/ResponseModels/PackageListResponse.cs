namespace Arlink28.Api.Features.Catalogue.ResponseModels;

public record PackageListResponse(IReadOnlyList<PackageCardResponse> Items, string? NextCursor);
