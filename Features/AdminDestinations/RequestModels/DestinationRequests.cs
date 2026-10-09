using System.Text.RegularExpressions;
using FluentValidation;

namespace Arlink28.Api.Features.AdminDestinations.RequestModels;

/// <summary>
/// Creates a draft. Send <c>ParentId</c> (a country) to add a place inside it; leave it out to add a
/// country, in which case <c>Country</c> (ISO 3166-1 alpha-2) is required. The slug comes from the name unless given.
/// </summary>
public record CreateDestinationRequest(
    string Name,
    string? Country = null,
    Guid? ParentId = null,
    string? Slug = null,
    string? Tagline = null,
    string? Summary = null,
    string? Description = null,
    string? BestTimeToVisit = null,
    double? Latitude = null,
    double? Longitude = null,
    int SortOrder = 0);

/// <summary>Every field is optional; only the ones sent change. An empty string clears a text field.</summary>
public record UpdateDestinationRequest(
    string? Name = null,
    string? Slug = null,
    string? Tagline = null,
    string? Summary = null,
    string? Description = null,
    string? BestTimeToVisit = null,
    double? Latitude = null,
    double? Longitude = null,
    int? SortOrder = null,
    string? HeroAlt = null,
    string? HeroCredit = null);

/// <summary>One attraction in the replace-all list. Send an existing Id to keep that attraction's photo. List order is display order.</summary>
public record AttractionInput(
    Guid? Id,
    string Name,
    string? Summary = null,
    string? PhotoAlt = null,
    string? PhotoCredit = null);

public static partial class DestinationRules
{
    [GeneratedRegex("^[a-z0-9]+(-[a-z0-9]+)*$")]
    public static partial Regex SlugPattern();
}

public class CreateDestinationRequestValidator : AbstractValidator<CreateDestinationRequest>
{
    public CreateDestinationRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Country).Matches("^[A-Za-z]{2}$").When(x => !string.IsNullOrEmpty(x.Country))
            .WithMessage("Use a two-letter country code, such as BW.");
        RuleFor(x => x.Slug).MaximumLength(100).Must(s => DestinationRules.SlugPattern().IsMatch(s!))
            .When(x => !string.IsNullOrEmpty(x.Slug)).WithMessage("Use lowercase letters, numbers and single hyphens.");
        RuleFor(x => x.Tagline).MaximumLength(160);
        RuleFor(x => x.Summary).MaximumLength(400);
        RuleFor(x => x.Description).MaximumLength(10000);
        RuleFor(x => x.BestTimeToVisit).MaximumLength(200);
        RuleFor(x => x.Latitude).InclusiveBetween(-90, 90).When(x => x.Latitude.HasValue);
        RuleFor(x => x.Longitude).InclusiveBetween(-180, 180).When(x => x.Longitude.HasValue);
        RuleFor(x => x.SortOrder).InclusiveBetween(0, 10000);
    }
}

public class UpdateDestinationRequestValidator : AbstractValidator<UpdateDestinationRequest>
{
    public UpdateDestinationRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100).When(x => x.Name is not null);
        RuleFor(x => x.Slug).MaximumLength(100).Must(s => DestinationRules.SlugPattern().IsMatch(s!))
            .When(x => x.Slug is not null).WithMessage("Use lowercase letters, numbers and single hyphens.");
        RuleFor(x => x.Tagline).MaximumLength(160);
        RuleFor(x => x.Summary).MaximumLength(400);
        RuleFor(x => x.Description).MaximumLength(10000);
        RuleFor(x => x.BestTimeToVisit).MaximumLength(200);
        RuleFor(x => x.HeroAlt).MaximumLength(250);
        RuleFor(x => x.HeroCredit).MaximumLength(250);
        RuleFor(x => x.Latitude).InclusiveBetween(-90, 90).When(x => x.Latitude.HasValue);
        RuleFor(x => x.Longitude).InclusiveBetween(-180, 180).When(x => x.Longitude.HasValue);
        RuleFor(x => x.SortOrder).InclusiveBetween(0, 10000).When(x => x.SortOrder.HasValue);
    }
}

public class AttractionInputValidator : AbstractValidator<AttractionInput>
{
    public AttractionInputValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(120);
        RuleFor(x => x.Summary).MaximumLength(300);
        RuleFor(x => x.PhotoAlt).MaximumLength(250);
        RuleFor(x => x.PhotoCredit).MaximumLength(250);
    }
}
