using Arlink28.Api.Data.Entities;
using Arlink28.Api.Features.AdminPackages.RequestModels;
using Arlink28.Api.Features.AdminPackages.ResponseModels;

namespace Arlink28.Api.Features.AdminPackages.Services.Interfaces;

/// <summary>What publishing changed, or what stopped it. Detail is null when the package does not exist.</summary>
public record PublishOutcome(AdminPackageDetail? Detail, IReadOnlyList<string> Missing);

/// <remarks>
/// The Replace methods swap a package's whole list in one go, as the form saves it. They return null
/// when the package does not exist (404); bad input throws AppException (400).
/// </remarks>
public interface IAdminPackageContentService
{
    Task<AdminReferenceResponse> ReferenceAsync(CancellationToken ct = default);

    Task<AdminPackageDetail?> ReplaceStaysAsync(Guid id, IReadOnlyList<StayInput> stays, CancellationToken ct = default);
    Task<AdminPackageDetail?> ReplaceFeaturesAsync(Guid id, IReadOnlyList<FeatureInput> features, CancellationToken ct = default);
    Task<AdminPackageDetail?> ReplaceRatesAsync(Guid id, IReadOnlyList<RateInput> rates, CancellationToken ct = default);
    Task<AdminPackageDetail?> ReplaceAddOnsAsync(Guid id, IReadOnlyList<AddOnInput> addOns, CancellationToken ct = default);

    /// <summary>What a package still needs before it can be published. Empty means it is ready.</summary>
    IReadOnlyList<string> PublishChecklist(Package package, DateOnly today);

    Task<PublishOutcome> PublishAsync(Guid id, CancellationToken ct = default);
    Task<AdminPackageDetail?> UnpublishAsync(Guid id, CancellationToken ct = default);
    Task<AdminPackageDetail?> ArchiveAsync(Guid id, CancellationToken ct = default);
}
