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
    [ProducesResponseType(typeof(PackageListResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListPackages([FromQuery] PackageListRequest request, CancellationToken ct)
    {
        var result = await catalogue.ListPackagesAsync(request, ct);
        return Ok(result);
    }

    [HttpGet("{slug}")]
    [ProducesResponseType(typeof(PackageDetailResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetPackage(string slug, CancellationToken ct)
    {
        var result = await catalogue.GetPackageAsync(slug, ct);
        if (result is null)
            return this.ApiProblem(StatusCodes.Status404NotFound, $"Package '{slug}' not found.");
        return Ok(result);
    }

    [HttpGet("{slug}/quote")]
    [ProducesResponseType(typeof(QuoteResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> QuotePackage(string slug, [FromQuery] QuoteRequest request, CancellationToken ct)
    {
        var result = await catalogue.QuotePackageAsync(slug, request, ct);
        return Ok(result);
    }
}
