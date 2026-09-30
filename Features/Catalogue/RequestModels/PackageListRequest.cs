namespace Arlink28.Api.Features.Catalogue.RequestModels;

public record PackageListRequest(
    string? Destination,
    string? Partner,
    string? Category,
    int? Adults,
    int? Children,
    bool? Featured,
    int Limit = 20,
    string? Cursor = null,
    /// <summary>1-based page number. When set, the list is paged (with a total) instead of cursor-walked.</summary>
    int? Page = null,
    /// <summary>featured (default), price (lowest first), -price (highest first) or nights (shortest first). Paged lists only.</summary>
    string? Sort = null,
    /// <summary>Matches part of the title, subtitle or summary.</summary>
    string? Q = null
);
