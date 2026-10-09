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
    /// <summary>Published destinations. Filter by ISO country code and by kind (Country or Place).</summary>
    [HttpGet("destinations")]
    [ProducesResponseType(typeof(IReadOnlyList<DestinationResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListDestinations(
        [FromQuery] string? country, [FromQuery] string? kind, CancellationToken ct)
    {
        var result = await catalogue.ListDestinationsAsync(country, kind, ct);
        return Ok(result);
    }

    /// <summary>The full profile of a published destination. Drafts are a 404.</summary>
    [HttpGet("destinations/{slug}")]
    [ProducesResponseType(typeof(DestinationDetailResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetDestination(string slug, CancellationToken ct)
    {
        var result = await catalogue.GetDestinationAsync(slug, ct);
        return result is null
            ? this.ApiProblem(StatusCodes.Status404NotFound, $"Destination '{slug}' not found.")
            : Ok(result);
    }

    [HttpGet("partners")]
    [ProducesResponseType(typeof(IReadOnlyList<PartnerResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListPartners(CancellationToken ct)
    {
        var result = await catalogue.ListPartnersAsync(ct);
        return Ok(result);
    }
}
