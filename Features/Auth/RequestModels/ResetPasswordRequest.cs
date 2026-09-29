using FluentValidation;

namespace Arlink28.Api.Features.Auth.RequestModels;

public record ResetPasswordRequest(string Email);

public class ResetPasswordRequestValidator : AbstractValidator<ResetPasswordRequest>
{
    public ResetPasswordRequestValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
    }
}
