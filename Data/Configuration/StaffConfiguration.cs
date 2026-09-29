using Arlink28.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Arlink28.Api.Data.Configuration;

public class StaffConfiguration : IEntityTypeConfiguration<Staff>
{
    public void Configure(EntityTypeBuilder<Staff> builder)
    {
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).HasDefaultValueSql("gen_random_uuid()");

        builder.HasIndex(s => s.Username).IsUnique();
        builder.HasIndex(s => s.Email).IsUnique();

        builder.Property(s => s.Username).HasMaxLength(50).IsRequired();
        builder.Property(s => s.Email).HasMaxLength(200).IsRequired();
        builder.Property(s => s.PasswordHash).IsRequired();
        builder.Property(s => s.Role).HasConversion<string>().HasMaxLength(20).IsRequired();

        builder.HasOne(s => s.InvitedBy)
            .WithMany()
            .HasForeignKey(s => s.InvitedById)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
