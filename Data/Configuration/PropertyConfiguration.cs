using Arlink28.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Arlink28.Api.Data.Configuration;

public class PropertyConfiguration : IEntityTypeConfiguration<Property>
{
    public void Configure(EntityTypeBuilder<Property> builder)
    {
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).HasDefaultValueSql("gen_random_uuid()");
        builder.HasIndex(p => p.Slug).IsUnique();
        builder.Property(p => p.Slug).HasMaxLength(100).IsRequired();
        builder.Property(p => p.Name).HasMaxLength(200).IsRequired();

        builder.HasOne(p => p.Partner)
            .WithMany(pt => pt.Properties)
            .HasForeignKey(p => p.PartnerId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(p => p.Destination)
            .WithMany(d => d.Properties)
            .HasForeignKey(p => p.DestinationId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
