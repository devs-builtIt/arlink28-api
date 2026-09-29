using Arlink28.Api.Data.Entities.Common;

namespace Arlink28.Api.Data.Entities;

public class Property : BaseAuditableEntity
{
    public string Slug { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;

    public Guid? PartnerId { get; set; }
    public Partner? Partner { get; set; }

    public Guid DestinationId { get; set; }
    public Destination Destination { get; set; } = null!;

    public ICollection<PackageStay> PackageStays { get; set; } = [];
    public ICollection<PropertyMedia> Media { get; set; } = [];
}
