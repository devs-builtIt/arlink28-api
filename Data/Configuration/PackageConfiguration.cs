using Arlink28.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Arlink28.Api.Data.Configuration;

public class PackageConfiguration : IEntityTypeConfiguration<Package>
{
    public void Configure(EntityTypeBuilder<Package> builder)
    {
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).HasDefaultValueSql("gen_random_uuid()");
        builder.HasIndex(p => p.Slug).IsUnique();
        builder.Property(p => p.Slug).HasMaxLength(100).IsRequired();
        builder.Property(p => p.ProductType).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(p => p.Details).HasColumnType("jsonb");
        builder.HasIndex(p => new { p.ProductType, p.Status });
        builder.Property(p => p.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(p => p.Title).HasMaxLength(200).IsRequired();
        builder.Property(p => p.Subtitle).HasMaxLength(200);
        builder.Property(p => p.Summary).HasMaxLength(500);
        builder.Property(p => p.Category).HasMaxLength(50).IsRequired();
        builder.Property(p => p.PricingBasis).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(p => p.BaseCurrency).HasMaxLength(3).IsRequired().IsFixedLength();
        builder.Property(p => p.SeoTitle).HasMaxLength(70);
        builder.Property(p => p.SeoDescription).HasMaxLength(160);

        builder.HasOne(p => p.Destination)
            .WithMany(d => d.Packages)
            .HasForeignKey(p => p.DestinationId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(p => new { p.Featured, p.SortOrder, p.Id });
    }
}
