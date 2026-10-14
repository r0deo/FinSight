using FinSight.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace FinSight.Infrastructure.Persistence.Configurations;

public sealed class BusinessCostConfiguration : IEntityTypeConfiguration<BusinessCost>
{
    public void Configure(EntityTypeBuilder<BusinessCost> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).IsRequired().HasMaxLength(100);
        builder.Property(x => x.Note).HasMaxLength(500);
        builder.Property(x => x.Amount).HasPrecision(14, 2);
        builder.Property(x => x.Category).HasConversion<string>().HasMaxLength(24);
        builder.Property(x => x.Certainty).HasConversion<string>().HasMaxLength(16);
        builder.Property(x => x.Inclusion).HasConversion<string>().HasMaxLength(16);
        builder.Property(x => x.Version).IsConcurrencyToken();
        builder.HasIndex(x => new { x.BusinessId, x.Month });
        builder.HasOne<Business>().WithMany().HasForeignKey(x => x.BusinessId).OnDelete(DeleteBehavior.Restrict);
    }
}
