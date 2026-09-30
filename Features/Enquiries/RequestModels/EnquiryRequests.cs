using Arlink28.Api.Data.Entities;
using FluentValidation;

namespace Arlink28.Api.Features.Enquiries.RequestModels;

/// <param name="Website">A honeypot: a field real guests never see. Anything in it marks a bot.</param>
public record CreateEnquiryRequest(
    EnquiryType Type,
    string? Slug,
    DateOnly? CheckIn,
    int? Nights,
    string Name,
    string Email,
    string? Phone,
    string? Subject,
    string? Message,
    bool Consent,
    string? SourceUrl,
    string? Website
);

public record UpdateEnquiryRequest(EnquiryStatus Status);

public class CreateEnquiryRequestValidator : AbstractValidator<CreateEnquiryRequest>
{
    public CreateEnquiryRequestValidator()
    {
        RuleFor(x => x.Type).IsInEnum();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(120);
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(200);
        RuleFor(x => x.Phone).MaximumLength(40);
        RuleFor(x => x.Subject).MaximumLength(200);
        RuleFor(x => x.Message).MaximumLength(4000);
        RuleFor(x => x.SourceUrl).MaximumLength(500);
        RuleFor(x => x.Consent).Equal(true).WithMessage("Please agree to the privacy policy so we can contact you.");

        // A package enquiry is about one package; anything else has to say what it is about.
        RuleFor(x => x.Slug).NotEmpty().MaximumLength(100).When(x => x.Type == EnquiryType.Package);
        RuleFor(x => x.Subject).NotEmpty().When(x => x.Type != EnquiryType.Package);
        RuleFor(x => x.Message).NotEmpty().When(x => x.Type != EnquiryType.Package);

        RuleFor(x => x.CheckIn)
            .Must(d => d!.Value >= DateOnly.FromDateTime(DateTime.UtcNow))
            .When(x => x.CheckIn.HasValue)
            .WithMessage("Check-in date cannot be in the past.");
        RuleFor(x => x.Nights).InclusiveBetween(1, 60).When(x => x.Nights.HasValue);
    }
}

public class UpdateEnquiryRequestValidator : AbstractValidator<UpdateEnquiryRequest>
{
    public UpdateEnquiryRequestValidator() => RuleFor(x => x.Status).IsInEnum();
}
