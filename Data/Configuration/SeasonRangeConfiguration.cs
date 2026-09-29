using Arlink28.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Arlink28.Api.Data.Configuration;

public class SeasonRangeConfiguration : IEntityTypeConfiguration<SeasonRange>
{
    public void Configure(EntityTypeBuilder<SeasonRange> builder)
    {
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).HasDefaultValueSql("gen_random_uuid()");

        builder.HasOne(r => r.Season)
            .WithMany(s => s.Ranges)
            .HasForeignKey(r => r.SeasonId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
