using System.Security.Claims;
using Arlink28.Api.Features.UserManagement.RequestModels;
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
    public async Task<IActionResult> ListUsers(CancellationToken ct)
    {
        var result = await users.ListUsersAsync(ct);
        return Ok(ApiResponse<object>.Ok(result));
    }

    [HttpPost("invite")]
    public async Task<IActionResult> InviteUser([FromBody] InviteUserRequest request, CancellationToken ct)
    {
        var result = await users.InviteUserAsync(request, CurrentUserId, ct);
        if (!result.Succeeded)
            return BadRequest(ApiResponse<object>.Fail(result.Errors[0]));
        return NoContent();
    }

    [AllowAnonymous]
    [HttpPost("invite/accept")]
    public async Task<IActionResult> AcceptInvite([FromBody] AcceptInviteRequest request, CancellationToken ct)
    {
        var result = await users.AcceptInviteAsync(request, ct);
        if (!result.Succeeded)
            return BadRequest(ApiResponse<object>.Fail(result.Errors[0]));
        return Ok(ApiResponse<object>.Ok(result.Value));
    }

    [HttpPatch("{id:guid}/role")]
    public async Task<IActionResult> AssignRole(Guid id, [FromBody] AssignRoleRequest request, CancellationToken ct)
    {
        var result = await users.AssignRoleAsync(id, request, CurrentUserId, ct);
        if (!result.Succeeded)
            return NotFound(ApiResponse<object>.Fail(result.Errors[0]));
        return NoContent();
    }

    [HttpPatch("{id:guid}/deactivate")]
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
