namespace Arlink28.Api.Features.Catalogue.RequestModels;

public record QuoteRequest(
    DateOnly CheckIn,
    int? Nights,
    string Currency = "USD",
    string? AddOns = null
);
