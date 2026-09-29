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
    string? HeroImagePath
);
