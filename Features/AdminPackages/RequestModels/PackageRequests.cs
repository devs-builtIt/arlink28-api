using Arlink28.Api.Data.Entities;
using FluentValidation;
using Newtonsoft.Json.Linq;

namespace Arlink28.Api.Features.AdminPackages.RequestModels;

public record CreatePackageRequest(
    string Title,
    Guid DestinationId,
    string Category,
    int Nights,
    int Adults,
    int Children,
    string? Subtitle,
    string? Summary,
    string? Description,
    PricingBasis? PricingBasis,
    string? BaseCurrency,
    /// <summary>Defaults to HolidayPackage. Can't be changed once created.</summary>
    ProductType? ProductType = null,
    /// <summary>The fields for a flight, hotel reservation or visa; ignored for holidays.</summary>
    JObject? Details = null,
    /// <summary>The "from" price in minor units, for the types without rates. Ignored for holidays.</summary>
    long? FromPriceMinor = null
);

/// <summary>Every field is optional; only the ones sent change.</summary>
public record UpdatePackageRequest(
    string? Title,
    Guid? DestinationId,
    string? Category,
    int? Nights,
    int? MinNights,
    int? Adults,
    int? Children,
    string? Subtitle,
    string? Summary,
    string? Description,
    PricingBasis? PricingBasis,
    string? BaseCurrency,
    bool? Featured,
    string? SeoTitle,
    string? SeoDescription,
    /// <summary>Replaces the type's details wholesale; rejected for holidays.</summary>
    JObject? Details = null,
    /// <summary>The "from" price in minor units for the types without rates; 0 clears it. Rejected for holidays.</summary>
    long? FromPriceMinor = null
);

public class CreatePackageRequestValidator : AbstractValidator<CreatePackageRequest>
{
    public CreatePackageRequestValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.DestinationId).NotEmpty();
        static bool IsHoliday(CreatePackageRequest x) => x.ProductType is null or ProductType.HolidayPackage;
        RuleFor(x => x.ProductType).IsInEnum().When(x => x.ProductType.HasValue);
        RuleFor(x => x.FromPriceMinor).GreaterThanOrEqualTo(0).When(x => x.FromPriceMinor.HasValue);
        RuleFor(x => x.Category).NotEmpty().When(IsHoliday).WithMessage("Choose a type.");
        RuleFor(x => x.Category).MaximumLength(50);
        RuleFor(x => x.Nights).InclusiveBetween(1, 60).When(IsHoliday);
        RuleFor(x => x.Nights).InclusiveBetween(0, 60).When(x => !IsHoliday(x));
        RuleFor(x => x.Adults).InclusiveBetween(1, 20).When(IsHoliday);
        RuleFor(x => x.Adults).InclusiveBetween(0, 20).When(x => !IsHoliday(x));
        RuleFor(x => x.Children).InclusiveBetween(0, 20);
        RuleFor(x => x.Subtitle).MaximumLength(300);
        RuleFor(x => x.Summary).MaximumLength(1000);
        RuleFor(x => x.Description).MaximumLength(10000);
        RuleFor(x => x.PricingBasis).IsInEnum();
        RuleFor(x => x.BaseCurrency).Length(3).When(x => x.BaseCurrency is not null);
    }
}

public class UpdatePackageRequestValidator : AbstractValidator<UpdatePackageRequest>
{
    public UpdatePackageRequestValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200).When(x => x.Title is not null);
        RuleFor(x => x.Category).NotEmpty().MaximumLength(50).When(x => x.Category is not null);
        RuleFor(x => x.Nights).InclusiveBetween(0, 60).When(x => x.Nights.HasValue);
        RuleFor(x => x.MinNights).InclusiveBetween(1, 60).When(x => x.MinNights.HasValue);
        RuleFor(x => x.FromPriceMinor).GreaterThanOrEqualTo(0).When(x => x.FromPriceMinor.HasValue);
        RuleFor(x => x.Adults).InclusiveBetween(0, 20).When(x => x.Adults.HasValue);
        RuleFor(x => x.Children).InclusiveBetween(0, 20).When(x => x.Children.HasValue);
        RuleFor(x => x.Subtitle).MaximumLength(300);
        RuleFor(x => x.Summary).MaximumLength(1000);
        RuleFor(x => x.Description).MaximumLength(10000);
        RuleFor(x => x.SeoTitle).MaximumLength(70);
        RuleFor(x => x.SeoDescription).MaximumLength(300);
        RuleFor(x => x.PricingBasis).IsInEnum().When(x => x.PricingBasis.HasValue);
        RuleFor(x => x.BaseCurrency).Length(3).When(x => x.BaseCurrency is not null);
    }
}
