using System.Security.Claims;
using Arlink28.Api.Features.Enquiries.RequestModels;
using Arlink28.Api.Features.Enquiries.ResponseModels;
using Arlink28.Api.Features.Enquiries.Services.Interfaces;
using Arlink28.Api.Helpers;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Arlink28.Api.Features.Enquiries.Controllers;

/// <summary>Staff-only: read enquiries and mark how far each one has got.</summary>
[ApiController]
[ApiVersion(1)]
[Authorize]
[Route("api/v{version:apiVersion}/admin/enquiries")]
public class AdminEnquiriesController(IEnquiryService enquiries) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(EnquiryListResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(
        [FromQuery] string? status, [FromQuery] string? type, [FromQuery] int page = 1, [FromQuery] int pageSize = 25,
        CancellationToken ct = default)
        => Ok(await enquiries.ListAsync(status, page, pageSize, type, ct));

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(EnquiryDetail), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var result = await enquiries.GetAsync(id, ct);
        return result is null ? NotFoundEnquiry() : Ok(result);
    }

    [HttpPatch("{id:guid}")]
    [ProducesResponseType(typeof(EnquiryDetail), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetStatus(Guid id, [FromBody] UpdateEnquiryRequest request, CancellationToken ct)
    {
        var result = await enquiries.SetStatusAsync(id, request, ActorId, ct);
        return result is null ? NotFoundEnquiry() : Ok(result);
    }

    private Guid ActorId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    private IActionResult NotFoundEnquiry() => this.ApiProblem(StatusCodes.Status404NotFound, "Enquiry not found.");
}
