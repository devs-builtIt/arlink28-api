using Arlink28.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Arlink28.Api.Data.Configuration;

public class SeasonConfiguration : IEntityTypeConfiguration<Season>
{
    public void Configure(EntityTypeBuilder<Season> builder)
    {
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).HasDefaultValueSql("gen_random_uuid()");
        builder.HasIndex(s => s.Slug).IsUnique();
        builder.Property(s => s.Slug).HasMaxLength(100).IsRequired();
        builder.Property(s => s.Name).HasMaxLength(100).IsRequired();

        builder.HasOne(s => s.Partner)
            .WithMany(p => p.Seasons)
            .HasForeignKey(s => s.PartnerId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
