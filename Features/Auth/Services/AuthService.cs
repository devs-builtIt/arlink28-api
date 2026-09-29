using System.Security.Cryptography;
using Arlink28.Api.Data;
using Arlink28.Api.Data.Entities;
using Arlink28.Api.Features.Auth.RequestModels;
using Arlink28.Api.Features.Auth.ResponseModels;
using Arlink28.Api.Features.Auth.Services.Interfaces;
using Arlink28.Api.Features.Shared.Interfaces;
using Arlink28.Api.Features.Shared.Services.Interfaces;
using Arlink28.Api.Helpers;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Arlink28.Api.Features.Auth.Services;

public class AuthService(
    ApplicationDbContext db,
    IPasswordHasher<Staff> passwordHasher,
    IJwtService jwtService,
    IEmailService emailService) : IAuthService, IScoped
{
    private const int MaxFailedAttempts = 5;
    private static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan ResetTokenExpiry = TimeSpan.FromHours(1);

    public async Task<Result<AuthResponse>> LoginAsync(LoginRequest request, CancellationToken ct = default)
    {
        var staff = await db.Staff.FirstOrDefaultAsync(s => s.Username == request.Username, ct);

        if (staff is null || !staff.IsActive)
            return Result<AuthResponse>.Failure("Invalid username or password.");

        if (staff.LockedUntil.HasValue && staff.LockedUntil > DateTime.UtcNow)
        {
            var remaining = (int)(staff.LockedUntil.Value - DateTime.UtcNow).TotalSeconds;
            return Result<AuthResponse>.Failure($"Account locked. Try again in {remaining} seconds.");
        }

        var verification = passwordHasher.VerifyHashedPassword(staff, staff.PasswordHash, request.Password);

        if (verification == PasswordVerificationResult.Failed)
        {
            staff.FailedLoginAttempts++;
            if (staff.FailedLoginAttempts >= MaxFailedAttempts)
            {
                staff.LockedUntil = DateTime.UtcNow.Add(LockoutDuration);
                staff.FailedLoginAttempts = 0;
                staff.UpdatedAt = DateTime.UtcNow;
                await db.SaveChangesAsync(ct);
                Audit("AccountLocked", staff.Id.ToString(), null);
                await db.SaveChangesAsync(ct);
                return Result<AuthResponse>.Failure($"Account locked for {LockoutDuration.TotalMinutes} minutes after too many failed attempts.");
            }
            staff.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
            return Result<AuthResponse>.Failure("Invalid username or password.");
        }

        if (verification == PasswordVerificationResult.SuccessRehashNeeded)
            staff.PasswordHash = passwordHasher.HashPassword(staff, request.Password);

        staff.FailedLoginAttempts = 0;
        staff.LockedUntil = null;
        staff.LastLoginAt = DateTime.UtcNow;
        staff.UpdatedAt = DateTime.UtcNow;
        Audit("StaffLogin", staff.Id.ToString(), staff.Id);
        await db.SaveChangesAsync(ct);

        var (token, expiresAt) = jwtService.IssueToken(staff);
        return Result<AuthResponse>.Success(new AuthResponse(token, staff.Role, staff.Username, expiresAt));
    }

    public async Task<Result<MeResponse>> GetCurrentAsync(Guid staffId, DateTime tokenExpiresAt, CancellationToken ct = default)
    {
        // A valid token isn't enough: the account may have been deactivated since it was issued.
        var staff = await db.Staff.AsNoTracking().FirstOrDefaultAsync(s => s.Id == staffId, ct);
        if (staff is null || !staff.IsActive)
            return Result<MeResponse>.Failure("Your session is no longer valid. Please sign in again.");

        return Result<MeResponse>.Success(
            new MeResponse(staff.Id, staff.Username, staff.Email, staff.Role, tokenExpiresAt));
    }

    public async Task LogoutAsync(Guid staffId, CancellationToken ct = default)
    {
        Audit("StaffLogout", staffId.ToString(), staffId);
        await db.SaveChangesAsync(ct);
    }

    public async Task<Result> ChangePasswordAsync(Guid staffId, ChangePasswordRequest request, CancellationToken ct = default)
    {
        var staff = await db.Staff.FindAsync([staffId], ct);
        if (staff is null || !staff.IsActive)
            return Result.Failure("Staff not found.");

        var verification = passwordHasher.VerifyHashedPassword(staff, staff.PasswordHash, request.CurrentPassword);
        if (verification == PasswordVerificationResult.Failed)
            return Result.Failure("Current password is incorrect.");

        staff.PasswordHash = passwordHasher.HashPassword(staff, request.NewPassword);
        staff.UpdatedAt = DateTime.UtcNow;
        Audit("PasswordChanged", staffId.ToString(), staffId);
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task RequestPasswordResetAsync(string email, CancellationToken ct = default)
    {
        var staff = await db.Staff.FirstOrDefaultAsync(s => s.Email == email && s.IsActive, ct);
        if (staff is null) return; // Never reveal whether email exists

        // Invalidate any existing unused tokens for this staff
        var existing = await db.StaffTokens
            .Where(t => t.StaffId == staff.Id && t.Type == StaffTokenType.PasswordReset && t.UsedAt == null)
            .ToListAsync(ct);
        foreach (var t in existing) t.UsedAt = DateTime.UtcNow;

        var (plain, tokenRecord) = CreateToken(StaffTokenType.PasswordReset, staff.Email, null, ResetTokenExpiry);
        tokenRecord.StaffId = staff.Id;
        db.StaffTokens.Add(tokenRecord);
        await db.SaveChangesAsync(ct);

        await emailService.SendPasswordResetAsync(email, plain, ct);
    }

    public async Task<Result> ConfirmPasswordResetAsync(ConfirmResetPasswordRequest request, CancellationToken ct = default)
    {
        var hash = HashToken(request.Token);
        var tokenRecord = await db.StaffTokens
            .Include(t => t.Staff)
            .FirstOrDefaultAsync(t =>
                t.TokenHash == hash &&
                t.Type == StaffTokenType.PasswordReset &&
                t.UsedAt == null &&
                t.ExpiresAt > DateTime.UtcNow, ct);

        if (tokenRecord?.Staff is null)
            return Result.Failure("Reset token is invalid or has expired.");

        var staff = tokenRecord.Staff;
        staff.PasswordHash = passwordHasher.HashPassword(staff, request.NewPassword);
        staff.UpdatedAt = DateTime.UtcNow;
        tokenRecord.UsedAt = DateTime.UtcNow;
        Audit("PasswordReset", staff.Id.ToString(), null);
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

    private void Audit(string action, string entityId, Guid? actorId)
    {
        db.AuditLogs.Add(new AuditLog
        {
            Action = action,
            EntityType = "Staff",
            EntityId = entityId,
            ActorId = actorId?.ToString(),
            CreatedAt = DateTime.UtcNow,
        });
    }
}
