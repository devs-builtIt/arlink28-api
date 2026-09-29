using Arlink28.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Arlink28.Api.Data.Configuration;

public class FeatureConfiguration : IEntityTypeConfiguration<Feature>
{
    public void Configure(EntityTypeBuilder<Feature> builder)
    {
        builder.HasKey(f => f.Id);
        builder.Property(f => f.Id).HasDefaultValueSql("gen_random_uuid()");
        builder.HasIndex(f => f.Slug).IsUnique();
        builder.Property(f => f.Slug).HasMaxLength(100).IsRequired();
        builder.Property(f => f.Label).HasMaxLength(200).IsRequired();
        builder.Property(f => f.Icon).HasMaxLength(100);
    }
}
