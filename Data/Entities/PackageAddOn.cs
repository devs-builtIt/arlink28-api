using Arlink28.Api.Data.Entities.Common;

namespace Arlink28.Api.Data.Entities;

public class PackageAddOn : BaseEntity
{
    public Guid PackageId { get; set; }
    public Package Package { get; set; } = null!;

    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public AddOnUnit Unit { get; set; }
    public string Currency { get; set; } = string.Empty;
    public long PriceMinor { get; set; }
    public int SortOrder { get; set; }
}
