namespace Arlink28.Api.Features.Catalogue.RequestModels;

public record PackageListRequest(
    string? Destination,
    string? Partner,
    string? Category,
    int? Adults,
    int? Children,
    bool? Featured,
    int Limit = 20,
    string? Cursor = null
);
