using Arlink28.Api.Features.Enquiries.RequestModels;
using Arlink28.Api.Features.Enquiries.ResponseModels;
using Arlink28.Api.Features.Enquiries.Services.Interfaces;
using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Arlink28.Api.Features.Enquiries.Controllers;

/// <summary>The public contact form. Anyone can post, so it is rate limited per address.</summary>
[ApiController]
[ApiVersion(1)]
[Route("api/v{version:apiVersion}/enquiries")]
public class EnquiriesController(IEnquiryService enquiries) : ControllerBase
{
    public const string RateLimitPolicy = "enquiries";

    [HttpPost]
    [EnableRateLimiting(RateLimitPolicy)]
    [ProducesResponseType(typeof(CreateEnquiryResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> Create([FromBody] CreateEnquiryRequest request, CancellationToken ct)
    {
        var result = await enquiries.CreateAsync(request, ct);
        return StatusCode(StatusCodes.Status201Created, result);
    }
}
