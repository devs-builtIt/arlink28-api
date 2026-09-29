using Arlink28.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Arlink28.Api.Data.Configuration;

public class PackageStayConfiguration : IEntityTypeConfiguration<PackageStay>
{
    public void Configure(EntityTypeBuilder<PackageStay> builder)
    {
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).HasDefaultValueSql("gen_random_uuid()");
        builder.Property(s => s.RoomType).HasMaxLength(100);

        builder.HasOne(s => s.Package)
            .WithMany(p => p.Stays)
            .HasForeignKey(s => s.PackageId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(s => s.Property)
            .WithMany(p => p.PackageStays)
            .HasForeignKey(s => s.PropertyId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
