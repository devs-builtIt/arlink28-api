using Arlink28.Api.Features.Auth.RequestModels;
using Arlink28.Api.Features.Auth.ResponseModels;
using Arlink28.Api.Helpers;

namespace Arlink28.Api.Features.Auth.Services.Interfaces;

public interface IAuthService
{
    Task<Result<AuthResponse>> LoginAsync(LoginRequest request, CancellationToken ct = default);
    Task LogoutAsync(Guid staffId, CancellationToken ct = default);
    Task<Result<MeResponse>> GetCurrentAsync(Guid staffId, DateTime tokenExpiresAt, CancellationToken ct = default);
    Task<Result> ChangePasswordAsync(Guid staffId, ChangePasswordRequest request, CancellationToken ct = default);
    Task RequestPasswordResetAsync(string email, CancellationToken ct = default);
    Task<Result> ConfirmPasswordResetAsync(ConfirmResetPasswordRequest request, CancellationToken ct = default);
}
