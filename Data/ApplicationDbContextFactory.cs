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

        // MIGRATION_CONNECTION names the target explicitly and beats everything else, including
        // user secrets (which override environment variables), so a migration can never hit the
        // dev database by accident. Otherwise prefer DirectConnection; fall back to
        // DefaultConnection (pooler) when the direct host isn't reachable (e.g. some office
        // networks block port 5432).
        var connectionString =
            Environment.GetEnvironmentVariable("MIGRATION_CONNECTION") ??
            configuration.GetConnectionString("DirectConnection") ??
            configuration.GetConnectionString("DefaultConnection") ??
            throw new InvalidOperationException("No connection string found.");

        var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();
        optionsBuilder.UseNpgsql(connectionString);

        return new ApplicationDbContext(optionsBuilder.Options);
    }
}
