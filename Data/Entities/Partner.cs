using Arlink28.Api.Data.Entities.Common;

namespace Arlink28.Api.Data.Entities;

public class Partner : BaseAuditableEntity
{
    public string Slug { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Tagline { get; set; }
    public string? LogoPath { get; set; }

    public ICollection<Property> Properties { get; set; } = [];
    public ICollection<Season> Seasons { get; set; } = [];
}
