using Arlink28.Api.Features.Catalogue.RequestModels;
using Arlink28.Api.Features.Catalogue.ResponseModels;

namespace Arlink28.Api.Features.Catalogue.Services.Interfaces;

public interface ICatalogueService
{
    Task<PackageListResponse> ListPackagesAsync(PackageListRequest request, CancellationToken ct = default);
    Task<PackageDetailResponse?> GetPackageAsync(string slug, CancellationToken ct = default);
    Task<QuoteResponse> QuotePackageAsync(string slug, QuoteRequest request, CancellationToken ct = default);
    Task<IReadOnlyList<DestinationResponse>> ListDestinationsAsync(CancellationToken ct = default);
    Task<IReadOnlyList<PartnerResponse>> ListPartnersAsync(CancellationToken ct = default);
}
