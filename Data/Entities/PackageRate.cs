using Arlink28.Api.Data.Entities.Common;

namespace Arlink28.Api.Data.Entities;

public class PackageRate : BaseEntity
{
    public Guid PackageId { get; set; }
    public Package Package { get; set; } = null!;

    public Guid SeasonId { get; set; }
    public Season Season { get; set; } = null!;

    public string Currency { get; set; } = string.Empty;
    public long PriceMinor { get; set; }
    public long? ExtraNightPriceMinor { get; set; }
}
