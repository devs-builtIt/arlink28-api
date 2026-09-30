using Arlink28.Api.Data.Entities;

namespace Arlink28.Api.Features.Enquiries.ResponseModels;

public record CreateEnquiryResponse(string Reference);

public record EnquiryListItem(
    Guid Id,
    string Reference,
    EnquiryType Type,
    EnquiryStatus Status,
    string? PackageTitle,
    string? Subject,
    DateOnly? CheckIn,
    int? Nights,
    long? QuotedTotalMinor,
    string? Currency,
    string Name,
    string Email,
    DateTime CreatedAt
);

/// <summary>How many enquiries sit in each status, whichever tab is open.</summary>
public record EnquiryStatusCounts(int All, int New, int Contacted, int Closed);

public record EnquiryListResponse(
    IReadOnlyList<EnquiryListItem> Items,
    int Total,
    int Page,
    int PageSize,
    EnquiryStatusCounts Counts
);

public record EnquiryDetail(
    Guid Id,
    string Reference,
    EnquiryType Type,
    EnquiryStatus Status,
    Guid? PackageId,
    string? PackageTitle,
    DateOnly? CheckIn,
    int? Nights,
    long? QuotedTotalMinor,
    string? Currency,
    string Name,
    string Email,
    string? Phone,
    string? Subject,
    string? Message,
    DateTime ConsentAt,
    string? SourceUrl,
    Guid? HandledById,
    DateTime CreatedAt,
    DateTime UpdatedAt
);
