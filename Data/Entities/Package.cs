using Arlink28.Api.Data.Entities.Common;

namespace Arlink28.Api.Data.Entities;

public class Package : BaseAuditableEntity
{
    public string Slug { get; set; } = string.Empty;
    public PackageStatus Status { get; set; } = PackageStatus.Draft;
    public string Title { get; set; } = string.Empty;
    public string? Subtitle { get; set; }
    public string? Summary { get; set; }
    public string? Description { get; set; }
    public string Category { get; set; } = string.Empty;

    public Guid DestinationId { get; set; }
    public Destination Destination { get; set; } = null!;

    public int Nights { get; set; }
    public int MinNights { get; set; }
    public int Adults { get; set; }
    public int Children { get; set; }
    public PricingBasis PricingBasis { get; set; } = PricingBasis.PerParty;
    public string BaseCurrency { get; set; } = "USD";
    public long? FromPriceMinor { get; set; }
    public bool Featured { get; set; } = false;
    public int SortOrder { get; set; } = 0;
    public string? SeoTitle { get; set; }
    public string? SeoDescription { get; set; }
    public int Version { get; set; } = 0;
    public DateTime? PublishedAt { get; set; }

    public ICollection<PackageStay> Stays { get; set; } = [];
    public ICollection<PackageFeature> Features { get; set; } = [];
    public ICollection<PackageRate> Rates { get; set; } = [];
    public ICollection<PackageAddOn> AddOns { get; set; } = [];
    public ICollection<PackageMedia> Media { get; set; } = [];
}
