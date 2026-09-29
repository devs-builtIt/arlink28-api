using Arlink28.Api.Data.Entities.Common;

namespace Arlink28.Api.Data.Entities;

public class Destination : BaseAuditableEntity
{
    public string Slug { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;

    public ICollection<Property> Properties { get; set; } = [];
    public ICollection<Package> Packages { get; set; } = [];
}
