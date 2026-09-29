using FluentValidation;

namespace Arlink28.Api.Features.Auth.RequestModels;

public record LoginRequest(string Username, string Password);

public class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(x => x.Username).NotEmpty();
        RuleFor(x => x.Password).NotEmpty();
    }
}
