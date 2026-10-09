using System.Security.Claims;
using Arlink28.Api.Features.AdminDestinations.RequestModels;
using Arlink28.Api.Features.AdminDestinations.ResponseModels;
using Arlink28.Api.Features.AdminDestinations.Services.Interfaces;
using Arlink28.Api.Features.AdminPackages.Services.Interfaces;
using Arlink28.Api.Helpers;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Arlink28.Api.Features.AdminDestinations.Controllers;

/// <summary>Staff-only destination management. Unlike the public catalogue, it sees drafts too.</summary>
[ApiController]
[ApiVersion(1)]
[Authorize]
[Route("api/v{version:apiVersion}/admin/destinations")]
public class AdminDestinationsController(IAdminDestinationService destinations) : ControllerBase
{
    private const long PhotoLimitBytes = 25L * 1024 * 1024;

    private string? ActorId => User.FindFirstValue(ClaimTypes.NameIdentifier);

    [HttpGet]
    [ProducesResponseType(typeof(AdminDestinationListResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(
        [FromQuery] string? status,
        [FromQuery] string? search,
        [FromQuery] string? country,
        [FromQuery] string? kind,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        CancellationToken ct = default)
        => Ok(await destinations.ListAsync(status, search, country, kind, page, pageSize, ct));

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(AdminDestinationDetail), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var result = await destinations.GetAsync(id, ct);
        return result is null ? NotFoundDestination() : Ok(result);
    }

    /// <summary>
    /// Creates a draft. Send <c>parentId</c> to add a place inside a country, or <c>country</c> (e.g. "BW") to
    /// add a country. 409 when the slug is taken.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(AdminDestinationDetail), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create([FromBody] CreateDestinationRequest request, CancellationToken ct)
    {
        var result = await destinations.CreateAsync(request, ActorId, ct);
        return CreatedAtAction(nameof(Get), new { id = result.Id }, result);
    }

    /// <summary>Changes the fields sent. The slug is locked once the destination has been published (409).</summary>
    [HttpPatch("{id:guid}")]
    [ProducesResponseType(typeof(AdminDestinationDetail), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateDestinationRequest request, CancellationToken ct)
    {
        var result = await destinations.UpdateAsync(id, request, ActorId, ct);
        return result is null ? NotFoundDestination() : Ok(result);
    }

    /// <summary>Replaces the attractions. List order is display order; send an existing Id to keep that attraction's photo.</summary>
    [HttpPut("{id:guid}/attractions")]
    [ProducesResponseType(typeof(AdminDestinationDetail), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ReplaceAttractions(Guid id, [FromBody] List<AttractionInput> attractions, CancellationToken ct)
    {
        var result = await destinations.ReplaceAttractionsAsync(id, attractions, ActorId, ct);
        return result is null ? NotFoundDestination() : Ok(result);
    }

    /// <summary>Sets the hero photo (form field <c>file</c>, JPEG/PNG/WebP), with optional <c>alt</c> and <c>credit</c> fields.</summary>
    [HttpPost("{id:guid}/hero")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(PhotoLimitBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = PhotoLimitBytes)]
    [ProducesResponseType(typeof(AdminDestinationDetail), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetHero(
        Guid id, IFormFile file, [FromForm] string? alt, [FromForm] string? credit, CancellationToken ct)
    {
        var upload = new UploadedImage(file.FileName, file.Length, file.OpenReadStream);
        var result = await destinations.SetHeroAsync(id, upload, alt, credit, ActorId, ct);
        return result is null ? NotFoundDestination() : Ok(result);
    }

    /// <summary>Sets one attraction's photo. Same form fields as the hero.</summary>
    [HttpPost("{id:guid}/attractions/{attractionId:guid}/photo")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(PhotoLimitBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = PhotoLimitBytes)]
    [ProducesResponseType(typeof(AdminDestinationDetail), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetAttractionPhoto(
        Guid id, Guid attractionId, IFormFile file, [FromForm] string? alt, [FromForm] string? credit, CancellationToken ct)
    {
        var upload = new UploadedImage(file.FileName, file.Length, file.OpenReadStream);
        var result = await destinations.SetAttractionPhotoAsync(id, attractionId, upload, alt, credit, ActorId, ct);
        return result is null ? NotFoundDestination() : Ok(result);
    }

    /// <summary>Publishes a draft. 422 with a `missing` list when it is not ready.</summary>
    [HttpPost("{id:guid}/publish")]
    [ProducesResponseType(typeof(AdminDestinationDetail), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Publish(Guid id, CancellationToken ct)
    {
        var outcome = await destinations.PublishAsync(id, ActorId, ct);
        if (outcome.Detail is null) return NotFoundDestination();
        if (outcome.Missing.Count == 0) return Ok(outcome.Detail);

        var problem = this.ApiProblem(StatusCodes.Status422UnprocessableEntity,
            "This destination is not ready to publish.", "PUBLISH_BLOCKED");
        ((ProblemDetails)problem.Value!).Extensions["missing"] = outcome.Missing;
        return problem;
    }

    /// <summary>Takes a destination off the site and back to draft.</summary>
    [HttpPost("{id:guid}/unpublish")]
    [ProducesResponseType(typeof(AdminDestinationDetail), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Unpublish(Guid id, CancellationToken ct)
    {
        var result = await destinations.UnpublishAsync(id, ActorId, ct);
        return result is null ? NotFoundDestination() : Ok(result);
    }

    /// <summary>Deletes a destination nothing uses. 409 while it still has places, packages or properties.</summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
        => await destinations.DeleteAsync(id, ActorId, ct) ? NoContent() : NotFoundDestination();

    private ObjectResult NotFoundDestination() => this.ApiProblem(StatusCodes.Status404NotFound, "Destination not found.");
}
