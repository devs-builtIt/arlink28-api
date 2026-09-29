using Arlink28.Api.Data.Entities.Common;

namespace Arlink28.Api.Data.Entities;

public class Staff : BaseAuditableEntity
{
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public StaffRole Role { get; set; }
    public bool IsActive { get; set; } = true;
    public int FailedLoginAttempts { get; set; } = 0;
    public DateTime? LockedUntil { get; set; }
    public DateTime? LastLoginAt { get; set; }

    public Guid? InvitedById { get; set; }
    public Staff? InvitedBy { get; set; }

    public ICollection<StaffToken> Tokens { get; set; } = [];
}
