using FinSight.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinSight.Infrastructure.Identity;

public sealed class ApplicationUserConfiguration
    : IEntityTypeConfiguration<ApplicationUser>
{
    public void Configure(EntityTypeBuilder<ApplicationUser> builder)
    {
        builder.Property(user => user.BusinessId)
            .IsRequired();

        builder.HasIndex(user => user.BusinessId);

        builder.HasOne<Business>()
            .WithMany()
            .HasForeignKey(user => user.BusinessId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
