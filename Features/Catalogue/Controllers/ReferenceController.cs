using Arlink28.Api.Features.Catalogue.ResponseModels;
using Arlink28.Api.Features.Catalogue.Services.Interfaces;
using Arlink28.Api.Helpers;
using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;

namespace Arlink28.Api.Features.Catalogue.Controllers;

[ApiController]
[ApiVersion(1)]
[Route("api/v{version:apiVersion}")]
public class ReferenceController(ICatalogueService catalogue) : ControllerBase
{
    [HttpGet("destinations")]
    [ProducesResponseType(typeof(IReadOnlyList<DestinationResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListDestinations(CancellationToken ct)
    {
        var result = await catalogue.ListDestinationsAsync(ct);
        return Ok(result);
    }

    [HttpGet("partners")]
    [ProducesResponseType(typeof(IReadOnlyList<PartnerResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListPartners(CancellationToken ct)
    {
        var result = await catalogue.ListPartnersAsync(ct);
        return Ok(result);
    }
}
