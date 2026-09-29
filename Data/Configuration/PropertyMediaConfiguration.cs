using Arlink28.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Arlink28.Api.Data.Configuration;

public class PropertyMediaConfiguration : IEntityTypeConfiguration<PropertyMedia>
{
    public void Configure(EntityTypeBuilder<PropertyMedia> builder)
    {
        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id).HasDefaultValueSql("gen_random_uuid()");
        builder.HasIndex(m => m.Path).IsUnique();
        builder.Property(m => m.Path).HasMaxLength(500).IsRequired();
        builder.Property(m => m.Alt).HasMaxLength(250);
        builder.Property(m => m.Caption).HasMaxLength(500);
        builder.Property(m => m.VideoProvider).HasConversion<string>().HasMaxLength(20);
        builder.Property(m => m.VideoId).HasMaxLength(100);

        builder.HasOne(m => m.Property)
            .WithMany(p => p.Media)
            .HasForeignKey(m => m.PropertyId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
