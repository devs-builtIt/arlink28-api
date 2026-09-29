using Arlink28.Api.Features.Auth.ResponseModels;
using Arlink28.Api.Features.UserManagement.RequestModels;
using Arlink28.Api.Features.UserManagement.ResponseModels;
using Arlink28.Api.Helpers;

namespace Arlink28.Api.Features.UserManagement.Services.Interfaces;

public interface IUserService
{
    Task<IReadOnlyList<UserResponse>> ListUsersAsync(CancellationToken ct = default);
    Task<Result> InviteUserAsync(InviteUserRequest request, Guid invitedById, CancellationToken ct = default);
    Task<Result<AuthResponse>> AcceptInviteAsync(AcceptInviteRequest request, CancellationToken ct = default);
    Task<Result> AssignRoleAsync(Guid userId, AssignRoleRequest request, Guid actorId, CancellationToken ct = default);
    Task<Result> DeactivateUserAsync(Guid userId, Guid actorId, CancellationToken ct = default);
}
