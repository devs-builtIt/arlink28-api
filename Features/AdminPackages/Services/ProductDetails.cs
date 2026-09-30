using Arlink28.Api.Data.Entities;
using Arlink28.Api.Helpers;
using FluentValidation;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;

namespace Arlink28.Api.Features.AdminPackages.Services;

public enum TripType { OneWay, Return, MultiCity }
public enum CabinClass { Economy, PremiumEconomy, Business, First }
public enum BoardBasis { RoomOnly, BedAndBreakfast, HalfBoard, FullBoard, AllInclusive }

/// <summary>Flights are quoted on request, so this describes the offer, not live fares.</summary>
public record FlightDetails(
    string? Origin,
    string? Destination,
    TripType? TripType,
    string? Airline,
    CabinClass? Cabin,
    string? Baggage,
    string? FareNotes,
    DateOnly? ValidUntil);

public record HotelDetails(
    Guid? PropertyId,
    string? HotelName,
    string? RoomType,
    BoardBasis? BoardBasis,
    string? CancellationTerms);

/// <summary>Our service fee and the government fee are kept apart so the site can show either, or neither.</summary>
public record VisaDetails(
    string? Country,
    string? VisaType,
    string? ProcessingTime,
    string? Validity,
    long? ServiceFeeMinor,
    long? GovernmentFeeMinor,
    string? Currency,
    List<string>? Requirements);

public class FlightDetailsValidator : AbstractValidator<FlightDetails>
{
    public FlightDetailsValidator()
    {
        RuleFor(x => x.Origin).NotEmpty().WithMessage("Say where the flight leaves from.");
        RuleFor(x => x.Destination).NotEmpty().WithMessage("Say where the flight goes to.");
        RuleFor(x => x.Origin).MaximumLength(100);
        RuleFor(x => x.Destination).MaximumLength(100);
        RuleFor(x => x.Airline).MaximumLength(100);
        RuleFor(x => x.Baggage).MaximumLength(300);
        RuleFor(x => x.FareNotes).MaximumLength(2000);
    }
}

public class HotelDetailsValidator : AbstractValidator<HotelDetails>
{
    public HotelDetailsValidator()
    {
        RuleFor(x => x).Must(x => x.PropertyId.HasValue || !string.IsNullOrWhiteSpace(x.HotelName))
            .WithMessage("Pick one of our properties or type the hotel's name.");
        RuleFor(x => x.HotelName).MaximumLength(200);
        RuleFor(x => x.RoomType).MaximumLength(100);
        RuleFor(x => x.CancellationTerms).MaximumLength(2000);
    }
}

public class VisaDetailsValidator : AbstractValidator<VisaDetails>
{
    public VisaDetailsValidator()
    {
        RuleFor(x => x.Country).NotEmpty().WithMessage("Say which country the visa is for.");
        RuleFor(x => x.VisaType).NotEmpty().WithMessage("Say which kind of visa this is.");
        RuleFor(x => x.Country).MaximumLength(100);
        RuleFor(x => x.VisaType).MaximumLength(100);
        RuleFor(x => x.ProcessingTime).MaximumLength(100);
        RuleFor(x => x.Validity).MaximumLength(100);
        RuleFor(x => x.ServiceFeeMinor).GreaterThanOrEqualTo(0).When(x => x.ServiceFeeMinor.HasValue);
        RuleFor(x => x.GovernmentFeeMinor).GreaterThanOrEqualTo(0).When(x => x.GovernmentFeeMinor.HasValue);
        RuleFor(x => x.Currency).Length(3).When(x => x.Currency is not null);
        RuleFor(x => x.Requirements).Must(r => r is null || r.Count <= 40).WithMessage("That is too many requirements; 40 at most.");
        RuleForEach(x => x.Requirements).NotEmpty().MaximumLength(300);
    }
}

/// <summary>
/// Holiday packages keep their data in tables; every other product type keeps its own fields as a JSON
/// object on the package. This turns what the admin sends into that JSON (dropping anything unknown) and
/// rejects what doesn't fit the type.
/// </summary>
public static class ProductDetails
{
    private static readonly JsonSerializerSettings Json = new()
    {
        ContractResolver = new CamelCasePropertyNamesContractResolver(),
        NullValueHandling = NullValueHandling.Ignore,
        Converters = { new StringEnumConverter() },
    };

    /// <summary>The JSON to store for <paramref name="type"/>, or null when the type has no details.</summary>
    public static string? Normalize(ProductType type, JObject? raw)
    {
        if (type == ProductType.HolidayPackage) return null;
        if (raw is null) throw new AppException("Fill in the details for this type first.");

        object details = type switch
        {
            ProductType.Flight => Read(raw, new FlightDetailsValidator()),
            ProductType.HotelReservation => Read(raw, new HotelDetailsValidator()),
            ProductType.VisaSupport => Read(raw, new VisaDetailsValidator()),
            _ => throw new AppException("Unknown product type."),
        };
        return JsonConvert.SerializeObject(details, Json);
    }

    public static JObject? ToJson(string? stored) =>
        string.IsNullOrWhiteSpace(stored) ? null : JObject.Parse(stored);

    private static T Read<T>(JObject raw, IValidator<T> validator) where T : class
    {
        T? parsed;
        try
        {
            parsed = raw.ToObject<T>(JsonSerializer.Create(Json));
        }
        catch (JsonException)
        {
            throw new AppException("Some of the details are not in a form we recognise.");
        }

        if (parsed is null) throw new AppException("Fill in the details for this type first.");
        var result = validator.Validate(parsed);
        if (!result.IsValid) throw new AppException(result.Errors[0].ErrorMessage);
        return parsed;
    }
}
