using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Arlink28.Api.Data;

public class ApplicationDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
{
    public ApplicationDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddEnvironmentVariables()
            .AddUserSecrets<ApplicationDbContextFactory>(optional: true)
            .Build();

        // Prefer DirectConnection for migrations; fall back to DefaultConnection (pooler)
        // when the direct host isn't reachable (e.g. some office networks block port 5432).
        var connectionString =
            configuration.GetConnectionString("DirectConnection") ??
            configuration.GetConnectionString("DefaultConnection") ??
            throw new InvalidOperationException("No connection string found.");

        var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();
        optionsBuilder.UseNpgsql(connectionString);

        return new ApplicationDbContext(optionsBuilder.Options);
    }
}
