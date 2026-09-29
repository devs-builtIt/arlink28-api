namespace Arlink28.Api.Features.Catalogue.ResponseModels;

public record QuoteLineResponse(string Label, long AmountMinor, string Currency);

public record QuoteResponse(
    long TotalMinor,
    string Currency,
    long BaseMinor,
    int Nights,
    IReadOnlyList<QuoteLineResponse> Lines
);
