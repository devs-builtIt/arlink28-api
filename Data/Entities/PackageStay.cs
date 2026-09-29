using Arlink28.Api.Data.Entities.Common;

namespace Arlink28.Api.Data.Entities;

public class PackageStay : BaseEntity
{
    public Guid PackageId { get; set; }
    public Package Package { get; set; } = null!;

    public Guid PropertyId { get; set; }
    public Property Property { get; set; } = null!;

    public int Nights { get; set; }
    public string? RoomType { get; set; }
    public int SortOrder { get; set; }
}
