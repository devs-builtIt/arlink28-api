using Arlink28.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Arlink28.Api.Data.Configuration;

public class DestinationConfiguration : IEntityTypeConfiguration<Destination>
{
    public void Configure(EntityTypeBuilder<Destination> builder)
    {
        builder.HasKey(d => d.Id);
        builder.Property(d => d.Id).HasDefaultValueSql("gen_random_uuid()");
        builder.HasIndex(d => d.Slug).IsUnique();
        builder.Property(d => d.Slug).HasMaxLength(100).IsRequired();
        builder.Property(d => d.Name).HasMaxLength(100).IsRequired();
        builder.Property(d => d.Country).HasMaxLength(2).IsRequired().IsFixedLength();

        builder.Property(d => d.Kind).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(d => d.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(d => d.Tagline).HasMaxLength(160);
        builder.Property(d => d.Summary).HasMaxLength(400);
        builder.Property(d => d.BestTimeToVisit).HasMaxLength(200);
        builder.Property(d => d.HeroPath).HasMaxLength(255);
        builder.Property(d => d.HeroAlt).HasMaxLength(250);
        builder.Property(d => d.HeroCredit).HasMaxLength(250);
        builder.HasIndex(d => new { d.Status, d.Country, d.SortOrder });

        builder.HasOne(d => d.Parent)
            .WithMany(d => d.Places)
            .HasForeignKey(d => d.ParentId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class DestinationAttractionConfiguration : IEntityTypeConfiguration<DestinationAttraction>
{
    public void Configure(EntityTypeBuilder<DestinationAttraction> builder)
    {
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).HasDefaultValueSql("gen_random_uuid()");
        builder.Property(a => a.Name).HasMaxLength(120).IsRequired();
        builder.Property(a => a.Summary).HasMaxLength(300);
        builder.Property(a => a.PhotoPath).HasMaxLength(255);
        builder.Property(a => a.PhotoAlt).HasMaxLength(250);
        builder.Property(a => a.PhotoCredit).HasMaxLength(250);
        builder.HasIndex(a => new { a.DestinationId, a.SortOrder });

        builder.HasOne(a => a.Destination)
            .WithMany(d => d.Attractions)
            .HasForeignKey(a => a.DestinationId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
