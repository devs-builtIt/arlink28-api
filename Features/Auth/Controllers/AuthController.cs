using System.Security.Claims;
using Arlink28.Api.Features.Auth.RequestModels;
using Arlink28.Api.Features.Auth.ResponseModels;
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
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken ct)
    {
        var result = await auth.LoginAsync(request, ct);
        if (!result.Succeeded)
            return this.ApiProblem(StatusCodes.Status401Unauthorized, result.Errors[0]);
        return Ok(result.Value);
    }

    /// <summary>The signed-in staff member. 401 if the token is invalid or the account has been deactivated.</summary>
    [Authorize]
    [HttpGet("me")]
    [ProducesResponseType(typeof(MeResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Me(CancellationToken ct)
    {
        if (!long.TryParse(User.FindFirstValue("exp"), out var exp))
            return this.ApiProblem(StatusCodes.Status401Unauthorized, "Invalid token.");
        var expiresAt = DateTimeOffset.FromUnixTimeSeconds(exp).UtcDateTime;

        var result = await auth.GetCurrentAsync(CurrentUserId, expiresAt, ct);
        if (!result.Succeeded)
            return this.ApiProblem(StatusCodes.Status401Unauthorized, result.Errors[0]);
        return Ok(result.Value);
    }

    [Authorize]
    [HttpPost("logout")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Logout(CancellationToken ct)
    {
        await auth.LogoutAsync(CurrentUserId, ct);
        return NoContent();
    }

    [Authorize]
    [HttpPatch("change-password")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request, CancellationToken ct)
    {
        var result = await auth.ChangePasswordAsync(CurrentUserId, request, ct);
        if (!result.Succeeded)
            return this.ApiProblem(StatusCodes.Status400BadRequest, result.Errors[0]);
        return NoContent();
    }

    [HttpPost("reset-password/request")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> ResetPasswordRequest([FromBody] ResetPasswordRequest request, CancellationToken ct)
    {
        await auth.RequestPasswordResetAsync(request.Email, ct);
        return NoContent(); // Always 204 — never reveal whether email exists
    }

    [HttpPost("reset-password/confirm")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ResetPasswordConfirm([FromBody] ConfirmResetPasswordRequest request, CancellationToken ct)
    {
        var result = await auth.ConfirmPasswordResetAsync(request, ct);
        if (!result.Succeeded)
            return this.ApiProblem(StatusCodes.Status400BadRequest, result.Errors[0]);
        return NoContent();
    }

    private Guid CurrentUserId =>
        Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
}
