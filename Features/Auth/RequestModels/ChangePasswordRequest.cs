using Arlink28.Api.Helpers;
using FluentValidation;

namespace Arlink28.Api.Features.Auth.RequestModels;

public record ChangePasswordRequest(string CurrentPassword, string NewPassword);

public class ChangePasswordRequestValidator : AbstractValidator<ChangePasswordRequest>
{
    public ChangePasswordRequestValidator()
    {
        RuleFor(x => x.CurrentPassword).NotEmpty();
        RuleFor(x => x.NewPassword).StrongPassword();
    }
}
