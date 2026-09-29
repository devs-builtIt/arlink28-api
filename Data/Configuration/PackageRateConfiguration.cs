using Arlink28.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Arlink28.Api.Data.Configuration;

public class PackageRateConfiguration : IEntityTypeConfiguration<PackageRate>
{
    public void Configure(EntityTypeBuilder<PackageRate> builder)
    {
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).HasDefaultValueSql("gen_random_uuid()");
        builder.Property(r => r.Currency).HasMaxLength(3).IsRequired().IsFixedLength();
        builder.HasIndex(r => new { r.PackageId, r.SeasonId, r.Currency }).IsUnique();

        builder.HasOne(r => r.Package)
            .WithMany(p => p.Rates)
            .HasForeignKey(r => r.PackageId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(r => r.Season)
            .WithMany(s => s.PackageRates)
            .HasForeignKey(r => r.SeasonId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
