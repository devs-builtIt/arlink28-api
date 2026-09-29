using Arlink28.Api.Data.Entities;

namespace Arlink28.Api.Helpers;

public record PricingRequest(
    DateOnly CheckIn,
    int? Nights,
    string Currency,
    IReadOnlyList<(Guid AddOnId, int Qty)> AddOns
);

public record QuoteLineItem(string Label, long AmountMinor, string Currency);

public record QuoteResult(
    long TotalMinor,
    string Currency,
    long BaseMinor,
    int Nights,
    IReadOnlyList<QuoteLineItem> Lines
);

public enum QuoteError
{
    CheckInInPast,
    BelowMinNights,
    ExtraNightsNotSold,
    NoRateForDate,
    CurrencyNotAvailable,
    UnknownAddOn
}

public class QuoteException(QuoteError code, string message) : Exception(message)
{
    public QuoteError Code { get; } = code;
}

public static class PricingEngine
{
    public static QuoteResult Quote(Package package, PricingRequest request)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        if (request.CheckIn < today)
            throw new QuoteException(QuoteError.CheckInInPast, "Check-in date cannot be in the past.");

        var nights = request.Nights ?? package.Nights;

        if (nights < package.MinNights)
            throw new QuoteException(QuoteError.BelowMinNights,
                $"Minimum stay is {package.MinNights} nights.");

        var extraNights = nights - package.Nights;
        if (extraNights > 0)
        {
            var anyRateHasExtra = package.Rates.Any(r => r.ExtraNightPriceMinor.HasValue);
            if (!anyRateHasExtra)
                throw new QuoteException(QuoteError.ExtraNightsNotSold,
                    "Extra nights are not available for this package.");
        }

        var rate = FindRate(package, request.CheckIn, request.Currency);

        var baseMinor = rate.PriceMinor;
        var lines = new List<QuoteLineItem>
        {
            new("Base package", baseMinor, request.Currency)
        };

        if (extraNights > 0 && rate.ExtraNightPriceMinor.HasValue)
        {
            var extraAmount = rate.ExtraNightPriceMinor.Value * extraNights;
            lines.Add(new($"{extraNights} extra night(s)", extraAmount, request.Currency));
            baseMinor += extraAmount;
        }

        foreach (var (addOnId, qty) in request.AddOns)
        {
            var addOn = package.AddOns.FirstOrDefault(a => a.Id == addOnId)
                ?? throw new QuoteException(QuoteError.UnknownAddOn, $"Add-on {addOnId} not found.");

            var addOnCurrency = addOn.Currency;
            var addOnAmount = addOn.PriceMinor * qty;
            lines.Add(new($"{addOn.Name} x{qty}", addOnAmount, addOnCurrency));
        }

        var totalMinor = lines
            .Where(l => l.Currency == request.Currency)
            .Sum(l => l.AmountMinor);

        return new QuoteResult(totalMinor, request.Currency, rate.PriceMinor, nights, lines);
    }

    private static PackageRate FindRate(Package package, DateOnly checkIn, string currency)
    {
        var matchingSeasonIds = package.Rates
            .Select(r => r.SeasonId)
            .Distinct()
            .Where(sid =>
            {
                var season = package.Rates.First(r => r.SeasonId == sid).Season;
                return season?.Ranges.Any(range =>
                    checkIn >= range.StartDate && checkIn <= range.EndDate) == true;
            })
            .ToList();

        if (matchingSeasonIds.Count == 0)
            throw new QuoteException(QuoteError.NoRateForDate,
                "No rate is defined for the requested check-in date.");

        var rate = package.Rates.FirstOrDefault(r =>
            matchingSeasonIds.Contains(r.SeasonId) &&
            r.Currency.Equals(currency, StringComparison.OrdinalIgnoreCase));

        if (rate is null)
            throw new QuoteException(QuoteError.CurrencyNotAvailable,
                $"Currency {currency} is not available for the matching season.");

        return rate;
    }
}
