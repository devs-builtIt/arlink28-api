using System.Security.Claims;
using Arlink28.Api.Features.Auth.RequestModels;
using Arlink28.Api.Features.Auth.Services.Interfaces;
using Arlink28.Api.Helpers;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Arlink28.Api.Features.Auth.Controllers;

[ApiController]
[ApiVersion(1)]
[Route("api/v{version:apiVersion}/auth")]
public class AuthController(IAuthService auth) : ControllerBase
{
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken ct)
    {
        var result = await auth.LoginAsync(request, ct);
        if (!result.Succeeded)
            return Unauthorized(ApiResponse<object>.Fail(result.Errors[0]));
        return Ok(ApiResponse<object>.Ok(result.Value));
    }

    [Authorize]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout(CancellationToken ct)
    {
        await auth.LogoutAsync(CurrentUserId, ct);
        return NoContent();
    }

    [Authorize]
    [HttpPatch("change-password")]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request, CancellationToken ct)
    {
        var result = await auth.ChangePasswordAsync(CurrentUserId, request, ct);
        if (!result.Succeeded)
            return BadRequest(ApiResponse<object>.Fail(result.Errors[0]));
        return NoContent();
    }

    [HttpPost("reset-password/request")]
    public async Task<IActionResult> ResetPasswordRequest([FromBody] ResetPasswordRequest request, CancellationToken ct)
    {
        await auth.RequestPasswordResetAsync(request.Email, ct);
        return NoContent(); // Always 204 — never reveal whether email exists
    }

    [HttpPost("reset-password/confirm")]
    public async Task<IActionResult> ResetPasswordConfirm([FromBody] ConfirmResetPasswordRequest request, CancellationToken ct)
    {
        var result = await auth.ConfirmPasswordResetAsync(request, ct);
        if (!result.Succeeded)
            return BadRequest(ApiResponse<object>.Fail(result.Errors[0]));
        return NoContent();
    }

    private Guid CurrentUserId =>
        Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
}
