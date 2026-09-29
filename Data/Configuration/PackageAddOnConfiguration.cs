using Arlink28.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Arlink28.Api.Data.Configuration;

public class PackageAddOnConfiguration : IEntityTypeConfiguration<PackageAddOn>
{
    public void Configure(EntityTypeBuilder<PackageAddOn> builder)
    {
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).HasDefaultValueSql("gen_random_uuid()");
        builder.Property(a => a.Name).HasMaxLength(200).IsRequired();
        builder.Property(a => a.Description).HasMaxLength(500);
        builder.Property(a => a.Unit).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(a => a.Currency).HasMaxLength(3).IsRequired().IsFixedLength();

        builder.HasOne(a => a.Package)
            .WithMany(p => p.AddOns)
            .HasForeignKey(a => a.PackageId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
