using Arlink28.Api.Helpers;
using FluentValidation;

namespace Arlink28.Api.Features.UserManagement.RequestModels;

public record AcceptInviteRequest(string Token, string Username, string Password);

public class AcceptInviteRequestValidator : AbstractValidator<AcceptInviteRequest>
{
    public AcceptInviteRequestValidator()
    {
        RuleFor(x => x.Token).NotEmpty();
        RuleFor(x => x.Username)
            .NotEmpty()
            .MinimumLength(3).WithMessage("Username must be at least 3 characters.")
            .MaximumLength(50)
            .Matches("^[a-zA-Z0-9_]+$").WithMessage("Username may only contain letters, digits, and underscores.");
        RuleFor(x => x.Password).StrongPassword();
    }
}
