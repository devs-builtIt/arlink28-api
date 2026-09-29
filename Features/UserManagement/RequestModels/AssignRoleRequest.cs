using Arlink28.Api.Data.Entities;
using FluentValidation;

namespace Arlink28.Api.Features.UserManagement.RequestModels;

public record AssignRoleRequest(StaffRole Role);

public class AssignRoleRequestValidator : AbstractValidator<AssignRoleRequest>
{
    public AssignRoleRequestValidator()
    {
        RuleFor(x => x.Role).IsInEnum();
    }
}
