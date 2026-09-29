using Arlink28.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Arlink28.Api.Data;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

    public virtual DbSet<Destination> Destinations { get; set; }
    public virtual DbSet<Partner> Partners { get; set; }
    public virtual DbSet<Property> Properties { get; set; }
    public virtual DbSet<Package> Packages { get; set; }
    public virtual DbSet<PackageStay> PackageStays { get; set; }
    public virtual DbSet<Feature> Features { get; set; }
    public virtual DbSet<PackageFeature> PackageFeatures { get; set; }
    public virtual DbSet<Season> Seasons { get; set; }
    public virtual DbSet<SeasonRange> SeasonRanges { get; set; }
    public virtual DbSet<PackageRate> PackageRates { get; set; }
    public virtual DbSet<PackageAddOn> PackageAddOns { get; set; }
    public virtual DbSet<PackageMedia> PackageMedia { get; set; }
    public virtual DbSet<PropertyMedia> PropertyMedia { get; set; }
    public virtual DbSet<AuditLog> AuditLogs { get; set; }
    public virtual DbSet<Staff> Staff { get; set; }
    public virtual DbSet<StaffToken> StaffTokens { get; set; }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
    }
}
