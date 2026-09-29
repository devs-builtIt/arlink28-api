using Arlink28.Api.Helpers;
using FluentValidation;

namespace Arlink28.Api.Features.Auth.RequestModels;

public record ConfirmResetPasswordRequest(string Token, string NewPassword);

public class ConfirmResetPasswordRequestValidator : AbstractValidator<ConfirmResetPasswordRequest>
{
    public ConfirmResetPasswordRequestValidator()
    {
        RuleFor(x => x.Token).NotEmpty();
        RuleFor(x => x.NewPassword).StrongPassword();
    }
}
