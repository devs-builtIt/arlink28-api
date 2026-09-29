using Arlink28.Api.Data.Entities;
using Arlink28.Api.Helpers.Settings;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Arlink28.Api.Data;

public class DataContextInitializer(
    ApplicationDbContext db,
    IPasswordHasher<Staff> passwordHasher,
    IOptions<AdminBootstrapSettings> bootstrap,
    ILogger<DataContextInitializer> logger)
{
    public async Task SeedSuperAdminAsync()
    {
        var settings = bootstrap.Value;
        if (!settings.Enabled) return;

        if (string.IsNullOrWhiteSpace(settings.Password) || settings.Password.StartsWith("PLACEHOLDER"))
        {
            logger.LogWarning("AdminBootstrap:Password is not set — skipping seed.");
            return;
        }

        var strategy = db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            if (await db.Staff.AnyAsync())
            {
                logger.LogInformation("SuperAdmin seed skipped — staff records already exist.");
                return;
            }

            var admin = new Staff
            {
                Id = Guid.NewGuid(),
                Username = settings.Username,
                Email = settings.Email,
                Role = StaffRole.SuperAdmin,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            };
            admin.PasswordHash = passwordHasher.HashPassword(admin, settings.Password);

            db.Staff.Add(admin);
            await db.SaveChangesAsync();
            logger.LogInformation("SuperAdmin '{Username}' seeded.", settings.Username);
        });
    }
}
