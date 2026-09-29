using Arlink28.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Arlink28.Api.Data.Configuration;

public class PackageFeatureConfiguration : IEntityTypeConfiguration<PackageFeature>
{
    public void Configure(EntityTypeBuilder<PackageFeature> builder)
    {
        builder.HasKey(pf => pf.Id);
        builder.Property(pf => pf.Id).HasDefaultValueSql("gen_random_uuid()");
        builder.Property(pf => pf.Section).HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(pf => pf.LabelOverride).HasMaxLength(200);
        builder.Property(pf => pf.Footnote).HasMaxLength(500);

        builder.HasOne(pf => pf.Package)
            .WithMany(p => p.Features)
            .HasForeignKey(pf => pf.PackageId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(pf => pf.Feature)
            .WithMany(f => f.PackageFeatures)
            .HasForeignKey(pf => pf.FeatureId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
