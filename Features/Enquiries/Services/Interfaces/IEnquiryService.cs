using Arlink28.Api.Features.Enquiries.RequestModels;
using Arlink28.Api.Features.Enquiries.ResponseModels;

namespace Arlink28.Api.Features.Enquiries.Services.Interfaces;

/// <remarks>
/// Get and status changes return null when the enquiry doesn't exist (404). Bad input throws
/// AppException; a package or date that can't be quoted throws QuoteException (422).
/// </remarks>
public interface IEnquiryService
{
    Task<CreateEnquiryResponse> CreateAsync(CreateEnquiryRequest request, CancellationToken ct = default);
    Task<EnquiryListResponse> ListAsync(string? status, int page, int pageSize, CancellationToken ct = default);
    Task<EnquiryDetail?> GetAsync(Guid id, CancellationToken ct = default);
    Task<EnquiryDetail?> SetStatusAsync(Guid id, UpdateEnquiryRequest request, Guid actorId, CancellationToken ct = default);
}
