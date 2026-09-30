using Arlink28.Api.Features.AdminPackages.RequestModels;
using Arlink28.Api.Features.AdminPackages.ResponseModels;
using Arlink28.Api.Features.AdminPackages.Services.Interfaces;
using Arlink28.Api.Features.Catalogue.ResponseModels;
using Arlink28.Api.Helpers;
using Arlink28.Api.Features.AdminPackages.RequestModels;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Arlink28.Api.Features.AdminPackages.Controllers;

/// <summary>Staff-only package management. Unlike the public catalogue, it sees drafts too.</summary>
[ApiController]
[ApiVersion(1)]
[Authorize]
[Route("api/v{version:apiVersion}/admin/packages")]
public class AdminPackagesController(IAdminPackageService packages, IAdminPackageContentService content) : ControllerBase
{
    private const long UploadLimitBytes = 250L * 1024 * 1024;

    [HttpGet]
    [ProducesResponseType(typeof(AdminPackageListResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(
        [FromQuery] string? status,
        [FromQuery] string? search,
        [FromQuery] string? destination,
        [FromQuery] string? category,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        CancellationToken ct = default)
        => Ok(await packages.ListAsync(status, search, destination, category, page, pageSize, ct));

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(AdminPackageDetail), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var result = await packages.GetAsync(id, ct);
        return result is null ? NotFoundPackage() : Ok(result);
    }

    /// <summary>Creates a draft. Drafts never appear in the public catalogue.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(AdminPackageDetail), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreatePackageRequest request, CancellationToken ct)
    {
        var result = await packages.CreateAsync(request, ct);
        return CreatedAtAction(nameof(Get), new { id = result.Id }, result);
    }

    [HttpPatch("{id:guid}")]
    [ProducesResponseType(typeof(AdminPackageDetail), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdatePackageRequest request, CancellationToken ct)
    {
        var result = await packages.UpdateAsync(id, request, ct);
        return result is null ? NotFoundPackage() : Ok(result);
    }

    /// <summary>
    /// Adds one or more photos (form field `files`, JPEG/PNG/WebP). The first photo of a package
    /// with none becomes its Hero; the rest go to the gallery. Nothing is saved if any file is rejected.
    /// </summary>
    [HttpPost("{id:guid}/media")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(UploadLimitBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = UploadLimitBytes)]
    [ProducesResponseType(typeof(IReadOnlyList<MediaResponse>), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AddPhotos(Guid id, [FromForm] List<IFormFile> files, CancellationToken ct)
    {
        var uploads = files.Select(f => new UploadedImage(f.FileName, f.Length, f.OpenReadStream)).ToList();
        var result = await packages.AddPhotosAsync(id, uploads, ct);
        return result is null ? NotFoundPackage() : StatusCode(StatusCodes.Status201Created, result);
    }

    /// <summary>Changes a photo's role (Hero, Gallery, Poster), alt text or caption.</summary>
    [HttpPatch("{id:guid}/media/{mediaId:guid}")]
    [ProducesResponseType(typeof(MediaResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateMedia(Guid id, Guid mediaId, [FromBody] UpdateMediaRequest request, CancellationToken ct)
    {
        var result = await packages.UpdateMediaAsync(id, mediaId, request, ct);
        return result is null ? NotFoundMedia() : Ok(result);
    }

    [HttpPut("{id:guid}/media/order")]
    [ProducesResponseType(typeof(IReadOnlyList<MediaResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ReorderMedia(Guid id, [FromBody] ReorderMediaRequest request, CancellationToken ct)
    {
        var result = await packages.ReorderMediaAsync(id, request, ct);
        return result is null ? NotFoundPackage() : Ok(result);
    }

    [HttpDelete("{id:guid}/media/{mediaId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteMedia(Guid id, Guid mediaId, CancellationToken ct)
        => await packages.DeleteMediaAsync(id, mediaId, ct) ? NoContent() : NotFoundMedia();

    /// <summary>The lists the package form picks from: destinations, properties, seasons and features.</summary>
    [HttpGet("~/api/v{version:apiVersion}/admin/reference")]
    [ProducesResponseType(typeof(AdminReferenceResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Reference(CancellationToken ct) => Ok(await content.ReferenceAsync(ct));

    /// <summary>Replaces the package's stays (where guests sleep, in order). Their nights should add up to the package's nights.</summary>
    [HttpPut("{id:guid}/stays")]
    [ProducesResponseType(typeof(AdminPackageDetail), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ReplaceStays(Guid id, [FromBody] List<StayInput> stays, CancellationToken ct)
    {
        var result = await content.ReplaceStaysAsync(id, stays, ct);
        return result is null ? NotFoundPackage() : Ok(result);
    }

    /// <summary>Replaces the inclusions, exclusions, highlights and notes.</summary>
    [HttpPut("{id:guid}/features")]
    [ProducesResponseType(typeof(AdminPackageDetail), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ReplaceFeatures(Guid id, [FromBody] List<FeatureInput> features, CancellationToken ct)
    {
        var result = await content.ReplaceFeaturesAsync(id, features, ct);
        return result is null ? NotFoundPackage() : Ok(result);
    }

    /// <summary>Replaces the season rates and recomputes the from-price. Seasons in one currency must not overlap.</summary>
    [HttpPut("{id:guid}/rates")]
    [ProducesResponseType(typeof(AdminPackageDetail), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ReplaceRates(Guid id, [FromBody] List<RateInput> rates, CancellationToken ct)
    {
        var result = await content.ReplaceRatesAsync(id, rates, ct);
        return result is null ? NotFoundPackage() : Ok(result);
    }

    /// <summary>Replaces the add-ons. Send an existing add-on's Id to keep it.</summary>
    [HttpPut("{id:guid}/add-ons")]
    [ProducesResponseType(typeof(AdminPackageDetail), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ReplaceAddOns(Guid id, [FromBody] List<AddOnInput> addOns, CancellationToken ct)
    {
        var result = await content.ReplaceAddOnsAsync(id, addOns, ct);
        return result is null ? NotFoundPackage() : Ok(result);
    }

    /// <summary>Publishes a draft. 422 with a `missing` list when it is not ready.</summary>
    [HttpPost("{id:guid}/publish")]
    [ProducesResponseType(typeof(AdminPackageDetail), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Publish(Guid id, CancellationToken ct)
    {
        var outcome = await content.PublishAsync(id, ct);
        if (outcome.Detail is null) return NotFoundPackage();
        if (outcome.Missing.Count == 0) return Ok(outcome.Detail);

        var problem = this.ApiProblem(StatusCodes.Status422UnprocessableEntity,
            "This package is not ready to publish.", "PUBLISH_BLOCKED");
        ((Microsoft.AspNetCore.Mvc.ProblemDetails)problem.Value!).Extensions["missing"] = outcome.Missing;
        return problem;
    }

    /// <summary>Takes a package off the site and back to draft.</summary>
    [HttpPost("{id:guid}/unpublish")]
    [ProducesResponseType(typeof(AdminPackageDetail), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Unpublish(Guid id, CancellationToken ct)
    {
        var result = await content.UnpublishAsync(id, ct);
        return result is null ? NotFoundPackage() : Ok(result);
    }

    [HttpPost("{id:guid}/archive")]
    [ProducesResponseType(typeof(AdminPackageDetail), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Archive(Guid id, CancellationToken ct)
    {
        var result = await content.ArchiveAsync(id, ct);
        return result is null ? NotFoundPackage() : Ok(result);
    }

    private ObjectResult NotFoundPackage() => this.ApiProblem(StatusCodes.Status404NotFound, "Package not found.");

    private ObjectResult NotFoundMedia() => this.ApiProblem(StatusCodes.Status404NotFound, "Photo not found.");
}
