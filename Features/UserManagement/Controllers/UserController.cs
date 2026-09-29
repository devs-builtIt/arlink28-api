using System.Security.Claims;
using Arlink28.Api.Features.Auth.ResponseModels;
using Arlink28.Api.Features.UserManagement.RequestModels;
using Arlink28.Api.Features.UserManagement.ResponseModels;
using Arlink28.Api.Features.UserManagement.Services.Interfaces;
using Arlink28.Api.Helpers;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Arlink28.Api.Features.UserManagement.Controllers;

[ApiController]
[ApiVersion(1)]
[Route("api/v{version:apiVersion}/users")]
[Authorize(Roles = "SuperAdmin")]
public class UserController(IUserService users) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<UserResponse>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(void), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(void), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ListUsers(CancellationToken ct)
    {
        var result = await users.ListUsersAsync(ct);
        return Ok(ApiResponse<IReadOnlyList<UserResponse>>.Ok(result));
    }

    [HttpPost("invite")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(void), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(void), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> InviteUser([FromBody] InviteUserRequest request, CancellationToken ct)
    {
        var result = await users.InviteUserAsync(request, CurrentUserId, ct);
        if (!result.Succeeded)
            return BadRequest(ApiResponse<object>.Fail(result.Errors[0]));
        return NoContent();
    }

    [AllowAnonymous]
    [HttpPost("invite/accept")]
    [ProducesResponseType(typeof(ApiResponse<AuthResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> AcceptInvite([FromBody] AcceptInviteRequest request, CancellationToken ct)
    {
        var result = await users.AcceptInviteAsync(request, ct);
        if (!result.Succeeded)
            return BadRequest(ApiResponse<object>.Fail(result.Errors[0]));
        return Ok(ApiResponse<AuthResponse>.Ok(result.Value));
    }

    [HttpPatch("{id:guid}/role")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(void), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(void), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> AssignRole(Guid id, [FromBody] AssignRoleRequest request, CancellationToken ct)
    {
        var result = await users.AssignRoleAsync(id, request, CurrentUserId, ct);
        if (!result.Succeeded)
            return NotFound(ApiResponse<object>.Fail(result.Errors[0]));
        return NoContent();
    }

    [HttpPatch("{id:guid}/deactivate")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(void), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(void), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> DeactivateUser(Guid id, CancellationToken ct)
    {
        var result = await users.DeactivateUserAsync(id, CurrentUserId, ct);
        if (!result.Succeeded)
            return BadRequest(ApiResponse<object>.Fail(result.Errors[0]));
        return NoContent();
    }

    private Guid CurrentUserId =>
        Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
}
