using Arlink28.Api.Data.Entities;
using FluentValidation;

namespace Arlink28.Api.Features.UserManagement.RequestModels;

public record InviteUserRequest(string Email, StaffRole Role);

public class InviteUserRequestValidator : AbstractValidator<InviteUserRequest>
{
    public InviteUserRequestValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.Role).IsInEnum();
    }
}
