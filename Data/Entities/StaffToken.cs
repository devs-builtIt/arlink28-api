using Arlink28.Api.Data.Entities.Common;

namespace Arlink28.Api.Data.Entities;

public class StaffToken : BaseEntity
{
    public StaffTokenType Type { get; set; }
    public string TokenHash { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public StaffRole? RoleToAssign { get; set; }

    public Guid? StaffId { get; set; }
    public Staff? Staff { get; set; }

    public DateTime ExpiresAt { get; set; }
    public DateTime? UsedAt { get; set; }
    public DateTime CreatedAt { get; set; }
}
