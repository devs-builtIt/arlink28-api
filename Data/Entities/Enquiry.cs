using Arlink28.Api.Data.Entities.Common;

namespace Arlink28.Api.Data.Entities;

/// <summary>A guest's enquiry from the public contact page, usually about one package.</summary>
public class Enquiry : BaseAuditableEntity
{
    /// <summary>What the guest is given, e.g. ENQ-2026-0042. Unique.</summary>
    public string Reference { get; set; } = string.Empty;
    public EnquiryType Type { get; set; }
    public EnquiryStatus Status { get; set; } = EnquiryStatus.New;

    public Guid? PackageId { get; set; }
    public Package? Package { get; set; }
    /// <summary>Copied at submit time so it survives a package being renamed.</summary>
    public string? PackageTitle { get; set; }
    public DateOnly? CheckIn { get; set; }
    public int? Nights { get; set; }
    /// <summary>The API's own quote, never the client's. Null when the date has no rate.</summary>
    public long? QuotedTotalMinor { get; set; }
    public string? Currency { get; set; }

    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? Subject { get; set; }
    public string? Message { get; set; }

    public DateTime ConsentAt { get; set; }
    public string? SourceUrl { get; set; }

    public Guid? HandledById { get; set; }
    public Staff? HandledBy { get; set; }
}
