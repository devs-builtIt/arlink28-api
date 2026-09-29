using System.Security.Cryptography;
using Arlink28.Api.Data;
using Arlink28.Api.Data.Entities;
using Arlink28.Api.Features.Auth.ResponseModels;
using Arlink28.Api.Features.Shared.Interfaces;
using Arlink28.Api.Features.Shared.Services.Interfaces;
using Arlink28.Api.Features.UserManagement.RequestModels;
using Arlink28.Api.Features.UserManagement.ResponseModels;
using Arlink28.Api.Features.UserManagement.Services.Interfaces;
using Arlink28.Api.Helpers;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Arlink28.Api.Features.UserManagement.Services;

public class UserService(
    ApplicationDbContext db,
    IPasswordHasher<Staff> passwordHasher,
    IJwtService jwtService,
    IEmailService emailService) : IUserService, IScoped
{
    private static readonly TimeSpan InviteExpiry = TimeSpan.FromHours(48);

    public async Task<IReadOnlyList<UserResponse>> ListUsersAsync(CancellationToken ct = default) =>
        await db.Staff
            .AsNoTracking()
            .OrderBy(s => s.Username)
            .Select(s => new UserResponse(s.Id, s.Username, s.Email, s.Role, s.IsActive, s.LastLoginAt, s.CreatedAt))
            .ToListAsync(ct);

    public async Task<Result> InviteUserAsync(InviteUserRequest request, Guid invitedById, CancellationToken ct = default)
    {
        if (await db.Staff.AnyAsync(s => s.Email == request.Email, ct))
            return Result.Failure("A user with that email already exists.");

        var pendingInvite = await db.StaffTokens.AnyAsync(t =>
            t.Email == request.Email &&
            t.Type == StaffTokenType.Invite &&
            t.UsedAt == null &&
            t.ExpiresAt > DateTime.UtcNow, ct);

        if (pendingInvite)
            return Result.Failure("An invite for that email is already pending.");

        var (plain, token) = CreateToken(StaffTokenType.Invite, request.Email, request.Role, InviteExpiry);
        db.StaffTokens.Add(token);
        Audit("UserInvited", request.Email, invitedById);
        await db.SaveChangesAsync(ct);

        await emailService.SendInviteAsync(request.Email, plain, ct);
        return Result.Success();
    }

    public async Task<Result<AuthResponse>> AcceptInviteAsync(AcceptInviteRequest request, CancellationToken ct = default)
    {
        var hash = HashToken(request.Token);
        var token = await db.StaffTokens.FirstOrDefaultAsync(t =>
            t.TokenHash == hash &&
            t.Type == StaffTokenType.Invite &&
            t.UsedAt == null &&
            t.ExpiresAt > DateTime.UtcNow, ct);

        if (token is null)
            return Result<AuthResponse>.Failure("Invite token is invalid or has expired.");

        if (await db.Staff.AnyAsync(s => s.Username == request.Username, ct))
            return Result<AuthResponse>.Failure("That username is already taken.");

        var staff = new Staff
        {
            Id = Guid.NewGuid(),
            Username = request.Username,
            Email = token.Email,
            Role = token.RoleToAssign!.Value,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        staff.PasswordHash = passwordHasher.HashPassword(staff, request.Password);

        token.UsedAt = DateTime.UtcNow;
        db.Staff.Add(staff);
        Audit("UserCreated", staff.Id.ToString(), null);
        await db.SaveChangesAsync(ct);

        var (jwtToken, expiresAt) = jwtService.IssueToken(staff);
        return Result<AuthResponse>.Success(new AuthResponse(jwtToken, staff.Role, staff.Username, expiresAt));
    }

    public async Task<Result> AssignRoleAsync(Guid userId, AssignRoleRequest request, Guid actorId, CancellationToken ct = default)
    {
        var staff = await db.Staff.FindAsync([userId], ct);
        if (staff is null)
            return Result.Failure("User not found.");

        var previous = staff.Role.ToString();
        staff.Role = request.Role;
        staff.UpdatedAt = DateTime.UtcNow;
        Audit("RoleChanged", userId.ToString(), actorId, $"{{\"from\":\"{previous}\",\"to\":\"{request.Role}\"}}");
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<Result> DeactivateUserAsync(Guid userId, Guid actorId, CancellationToken ct = default)
    {
        var staff = await db.Staff.FindAsync([userId], ct);
        if (staff is null)
            return Result.Failure("User not found.");

        if (!staff.IsActive)
            return Result.Failure("User is already deactivated.");

        staff.IsActive = false;
        staff.UpdatedAt = DateTime.UtcNow;
        Audit("UserDeactivated", userId.ToString(), actorId);
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }

    // ── helpers ──────────────────────────────────────────────────────────────

    private static (string Plain, StaffToken Token) CreateToken(
        StaffTokenType type, string email, StaffRole? role, TimeSpan expiry)
    {
        var plain = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .Replace("+", "-").Replace("/", "_").TrimEnd('=');
        var token = new StaffToken
        {
            Id = Guid.NewGuid(),
            Type = type,
            TokenHash = HashToken(plain),
            Email = email,
            RoleToAssign = role,
            ExpiresAt = DateTime.UtcNow.Add(expiry),
            CreatedAt = DateTime.UtcNow,
        };
        return (plain, token);
    }

    private static string HashToken(string plain)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(plain);
        return Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    }

    private void Audit(string action, string entityId, Guid? actorId, string? after = null)
    {
        db.AuditLogs.Add(new AuditLog
        {
            Id = Guid.NewGuid(),
            Action = action,
            EntityType = "Staff",
            EntityId = entityId,
            ActorId = actorId?.ToString(),
            After = after,
            CreatedAt = DateTime.UtcNow,
        });
    }
}
