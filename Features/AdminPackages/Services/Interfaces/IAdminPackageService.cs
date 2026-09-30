using Arlink28.Api.Features.AdminPackages.RequestModels;
using Arlink28.Api.Features.AdminPackages.ResponseModels;
using Arlink28.Api.Features.Catalogue.ResponseModels;

namespace Arlink28.Api.Features.AdminPackages.Services.Interfaces;

/// <summary>An uploaded file, read once. Kept apart from IFormFile so the service is testable.</summary>
public record UploadedImage(string FileName, long Length, Func<Stream> Open);

/// <remarks>
/// Methods return null when the package or media doesn't exist (404). Bad input throws
/// AppException, which the exception middleware turns into a 400 Problem Details.
/// </remarks>
public interface IAdminPackageService
{
    Task<AdminPackageListResponse> ListAsync(
        string? status, string? search, string? destination, string? category, int page, int pageSize, CancellationToken ct = default);
    Task<AdminPackageDetail?> GetAsync(Guid id, CancellationToken ct = default);
    Task<AdminPackageDetail> CreateAsync(CreatePackageRequest request, CancellationToken ct = default);
    Task<AdminPackageDetail?> UpdateAsync(Guid id, UpdatePackageRequest request, CancellationToken ct = default);

    Task<IReadOnlyList<MediaResponse>?> AddPhotosAsync(Guid id, IReadOnlyList<UploadedImage> files, CancellationToken ct = default);
    Task<MediaResponse?> UpdateMediaAsync(Guid id, Guid mediaId, UpdateMediaRequest request, CancellationToken ct = default);
    Task<IReadOnlyList<MediaResponse>?> ReorderMediaAsync(Guid id, ReorderMediaRequest request, CancellationToken ct = default);
    Task<bool> DeleteMediaAsync(Guid id, Guid mediaId, CancellationToken ct = default);
}
