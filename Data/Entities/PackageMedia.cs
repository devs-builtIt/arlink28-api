using Arlink28.Api.Data.Entities.Common;

namespace Arlink28.Api.Data.Entities;

public class PackageMedia : BaseEntity
{
    public Guid PackageId { get; set; }
    public Package Package { get; set; } = null!;

    public MediaRole Role { get; set; }
    public string Path { get; set; } = string.Empty;
    public string? Alt { get; set; }
    public string? Caption { get; set; }
    public int? Width { get; set; }
    public int? Height { get; set; }
    public VideoProvider? VideoProvider { get; set; }
    public string? VideoId { get; set; }
    public bool VariantsReady { get; set; } = false;
    public int SortKey { get; set; }
    public DateTime CreatedAt { get; set; }
}
