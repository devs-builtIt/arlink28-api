using Arlink28.Api.Data.Entities.Common;

namespace Arlink28.Api.Data.Entities;

public class PackageFeature : BaseEntity
{
    public Guid PackageId { get; set; }
    public Package Package { get; set; } = null!;

    public FeatureSection Section { get; set; }

    public Guid? FeatureId { get; set; }
    public Feature? Feature { get; set; }

    public string? LabelOverride { get; set; }
    public string? Footnote { get; set; }
    public int SortOrder { get; set; }
}
