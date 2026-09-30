using Arlink28.Api.Data.Entities;
using FluentValidation;

namespace Arlink28.Api.Features.AdminPackages.RequestModels;

/// <summary>Setting the role to Hero makes this photo the package's only Hero.</summary>
public record UpdateMediaRequest(MediaRole? Role, string? Alt, string? Caption);

/// <summary>Every media id of the package, in the order they should appear.</summary>
public record ReorderMediaRequest(IReadOnlyList<Guid> Ids);

public class UpdateMediaRequestValidator : AbstractValidator<UpdateMediaRequest>
{
    public UpdateMediaRequestValidator()
    {
        RuleFor(x => x.Role).IsInEnum().When(x => x.Role.HasValue);
        RuleFor(x => x.Alt).MaximumLength(250);
        RuleFor(x => x.Caption).MaximumLength(500);
    }
}
