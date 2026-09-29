using Arlink28.Api.Data.Entities.Common;

namespace Arlink28.Api.Data.Entities;

public class Feature : BaseEntity
{
    public string Slug { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string? Icon { get; set; }
    public DateTime CreatedAt { get; set; }

    public ICollection<PackageFeature> PackageFeatures { get; set; } = [];
}
