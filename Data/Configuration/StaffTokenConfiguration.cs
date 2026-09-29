using Arlink28.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Arlink28.Api.Data.Configuration;

public class StaffTokenConfiguration : IEntityTypeConfiguration<StaffToken>
{
    public void Configure(EntityTypeBuilder<StaffToken> builder)
    {
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).HasDefaultValueSql("gen_random_uuid()");

        builder.HasIndex(t => t.TokenHash).IsUnique();
        builder.HasIndex(t => new { t.Email, t.Type });

        builder.Property(t => t.Type).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(t => t.TokenHash).HasMaxLength(64).IsRequired();
        builder.Property(t => t.Email).HasMaxLength(200).IsRequired();
        builder.Property(t => t.RoleToAssign).HasConversion<string>().HasMaxLength(20);

        builder.HasOne(t => t.Staff)
            .WithMany(s => s.Tokens)
            .HasForeignKey(t => t.StaffId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
