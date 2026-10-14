using Arlink28.Api.Features.Dashboard.ResponseModels;
using Arlink28.Api.Features.Dashboard.Services.Interfaces;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Arlink28.Api.Features.Dashboard.Controllers;

/// <summary>Staff-only: the numbers behind the admin overview.</summary>
[ApiController]
[ApiVersion(1)]
[Authorize]
[Route("api/v{version:apiVersion}/admin/dashboard")]
public class AdminDashboardController(IDashboardService dashboard) : ControllerBase
{
    /// <param name="days">How many days to look back, 7 to 365. Defaults to 30.</param>
    [HttpGet]
    [ProducesResponseType(typeof(DashboardResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Get([FromQuery] int days = 30, CancellationToken ct = default)
        => Ok(await dashboard.GetAsync(days, ct));
}
