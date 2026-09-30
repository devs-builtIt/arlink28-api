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
    [ProducesResponseType(typeof(IReadOnlyList<UserResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ListUsers(CancellationToken ct)
    {
        var result = await users.ListUsersAsync(ct);
        return Ok(result);
    }

    [HttpPost("invite")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> InviteUser([FromBody] InviteUserRequest request, CancellationToken ct)
    {
        var result = await users.InviteUserAsync(request, CurrentUserId, ct);
        if (!result.Succeeded)
            return this.ApiProblem(StatusCodes.Status400BadRequest, result.Errors[0]);
        return NoContent();
    }

    [AllowAnonymous]
    [HttpPost("invite/accept")]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> AcceptInvite([FromBody] AcceptInviteRequest request, CancellationToken ct)
    {
        var result = await users.AcceptInviteAsync(request, ct);
        if (!result.Succeeded)
            return this.ApiProblem(StatusCodes.Status400BadRequest, result.Errors[0]);
        return Ok(result.Value);
    }

    [HttpPatch("{id:guid}/role")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> AssignRole(Guid id, [FromBody] AssignRoleRequest request, CancellationToken ct)
    {
        var result = await users.AssignRoleAsync(id, request, CurrentUserId, ct);
        if (!result.Succeeded)
            return this.ApiProblem(StatusCodes.Status404NotFound, result.Errors[0]);
        return NoContent();
    }

    [HttpPatch("{id:guid}/deactivate")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> DeactivateUser(Guid id, CancellationToken ct)
    {
        var result = await users.DeactivateUserAsync(id, CurrentUserId, ct);
        if (!result.Succeeded)
            return this.ApiProblem(StatusCodes.Status400BadRequest, result.Errors[0]);
        return NoContent();
    }

    private Guid CurrentUserId =>
        Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
}
