using Arlink28.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Arlink28.Api.Data.Configuration;

public class EnquiryConfiguration : IEntityTypeConfiguration<Enquiry>
{
    public void Configure(EntityTypeBuilder<Enquiry> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasDefaultValueSql("gen_random_uuid()");

        builder.HasIndex(e => e.Reference).IsUnique();
        builder.HasIndex(e => new { e.Status, e.CreatedAt });

        builder.Property(e => e.Reference).HasMaxLength(20).IsRequired();
        builder.Property(e => e.Type).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(e => e.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(e => e.PackageTitle).HasMaxLength(200);
        builder.Property(e => e.Currency).HasMaxLength(3).IsFixedLength();
        builder.Property(e => e.Name).HasMaxLength(120).IsRequired();
        builder.Property(e => e.Email).HasMaxLength(200).IsRequired();
        builder.Property(e => e.Phone).HasMaxLength(40);
        builder.Property(e => e.Subject).HasMaxLength(200);
        builder.Property(e => e.Message).HasMaxLength(4000);
        builder.Property(e => e.SourceUrl).HasMaxLength(500);

        // An enquiry outlives the package or staff member it points at.
        builder.HasOne(e => e.Package).WithMany().HasForeignKey(e => e.PackageId).OnDelete(DeleteBehavior.SetNull);
        builder.HasOne(e => e.HandledBy).WithMany().HasForeignKey(e => e.HandledById).OnDelete(DeleteBehavior.SetNull);
    }
}
