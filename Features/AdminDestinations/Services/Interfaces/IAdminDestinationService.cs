using Arlink28.Api.Features.AdminDestinations.RequestModels;
using Arlink28.Api.Features.AdminDestinations.ResponseModels;
using Arlink28.Api.Features.AdminPackages.Services.Interfaces;

namespace Arlink28.Api.Features.AdminDestinations.Services.Interfaces;

/// <summary>What publishing changed, or what stopped it. Detail is null when the destination does not exist.</summary>
public record DestinationPublishOutcome(AdminDestinationDetail? Detail, IReadOnlyList<string> Missing);

/// <remarks>
/// Methods return null when the destination does not exist (404). Bad input throws AppException (400);
/// a clash with current state (taken slug, destination still in use) throws ConflictException (409).
/// </remarks>
public interface IAdminDestinationService
{
    Task<AdminDestinationListResponse> ListAsync(
        string? status, string? search, string? country, string? kind, int page, int pageSize, CancellationToken ct = default);
    Task<AdminDestinationDetail?> GetAsync(Guid id, CancellationToken ct = default);
    Task<AdminDestinationDetail> CreateAsync(CreateDestinationRequest request, string? actorId, CancellationToken ct = default);
    Task<AdminDestinationDetail?> UpdateAsync(Guid id, UpdateDestinationRequest request, string? actorId, CancellationToken ct = default);
    Task<AdminDestinationDetail?> ReplaceAttractionsAsync(Guid id, IReadOnlyList<AttractionInput> attractions, string? actorId, CancellationToken ct = default);
    Task<AdminDestinationDetail?> SetHeroAsync(Guid id, UploadedImage file, string? alt, string? credit, string? actorId, CancellationToken ct = default);
    Task<AdminDestinationDetail?> SetAttractionPhotoAsync(Guid id, Guid attractionId, UploadedImage file, string? alt, string? credit, string? actorId, CancellationToken ct = default);
    Task<DestinationPublishOutcome> PublishAsync(Guid id, string? actorId, CancellationToken ct = default);
    Task<AdminDestinationDetail?> UnpublishAsync(Guid id, string? actorId, CancellationToken ct = default);
    /// <summary>False when it does not exist. Throws ConflictException while places, packages or properties still use it.</summary>
    Task<bool> DeleteAsync(Guid id, string? actorId, CancellationToken ct = default);
}
