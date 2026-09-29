using Arlink28.Api.Features.Catalogue.RequestModels;
using Arlink28.Api.Features.Catalogue.ResponseModels;
using Arlink28.Api.Features.Catalogue.Services.Interfaces;
using Arlink28.Api.Helpers;
using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;

namespace Arlink28.Api.Features.Catalogue.Controllers;

[ApiController]
[ApiVersion(1)]
[Route("api/v{version:apiVersion}/packages")]
public class PackagesController(ICatalogueService catalogue) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<PackageListResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListPackages([FromQuery] PackageListRequest request, CancellationToken ct)
    {
        var result = await catalogue.ListPackagesAsync(request, ct);
        return Ok(ApiResponse<PackageListResponse>.Ok(result));
    }

    [HttpGet("{slug}")]
    [ProducesResponseType(typeof(ApiResponse<PackageDetailResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetPackage(string slug, CancellationToken ct)
    {
        var result = await catalogue.GetPackageAsync(slug, ct);
        if (result is null)
            return NotFound(ApiResponse<object>.Fail($"Package '{slug}' not found."));
        return Ok(ApiResponse<PackageDetailResponse>.Ok(result));
    }

    [HttpGet("{slug}/quote")]
    [ProducesResponseType(typeof(ApiResponse<QuoteResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> QuotePackage(string slug, [FromQuery] QuoteRequest request, CancellationToken ct)
    {
        var result = await catalogue.QuotePackageAsync(slug, request, ct);
        return Ok(ApiResponse<QuoteResponse>.Ok(result));
    }
}
