namespace Arlink28.Api.Features.Catalogue.ResponseModels;

public record PackageCardResponse(
    Guid Id,
    string Slug,
    string Title,
    string? Subtitle,
    string? Summary,
    string Category,
    DestinationResponse Destination,
    int Nights,
    int Adults,
    int Children,
    string BaseCurrency,
    long? FromPriceMinor,
    bool Featured,
    string? HeroImagePath,
    /// <summary>Up to three lines worth showing on a card, highlights first.</summary>
    IReadOnlyList<string> Highlights,
    /// <summary>The lodges and camps, in the order guests visit them.</summary>
    IReadOnlyList<string> Lodges
);
