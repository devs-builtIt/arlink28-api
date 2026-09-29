using Arlink28.Api.Data.Entities.Common;

namespace Arlink28.Api.Data.Entities;

public class Season : BaseAuditableEntity
{
    public string Slug { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;

    public Guid? PartnerId { get; set; }
    public Partner? Partner { get; set; }

    public ICollection<SeasonRange> Ranges { get; set; } = [];
    public ICollection<PackageRate> PackageRates { get; set; } = [];
}
