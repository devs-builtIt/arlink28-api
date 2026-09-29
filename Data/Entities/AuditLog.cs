using Arlink28.Api.Data.Entities.Common;

namespace Arlink28.Api.Data.Entities;

public class AuditLog : BaseEntity
{
    public string? ActorId { get; set; }
    public string Action { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public string EntityId { get; set; } = string.Empty;
    public string? Before { get; set; }
    public string? After { get; set; }
    public DateTime CreatedAt { get; set; }
}
