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
    }
}
