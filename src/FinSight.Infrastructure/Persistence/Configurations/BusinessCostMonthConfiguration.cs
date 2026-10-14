using FinSight.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinSight.Infrastructure.Persistence.Configurations;

public sealed class BusinessCostMonthConfiguration : IEntityTypeConfiguration<BusinessCostMonth>
{
    public void Configure(EntityTypeBuilder<BusinessCostMonth> builder)
    {
        builder.HasKey(x => new { x.BusinessId, x.Month });
        builder.Property(x => x.Version).IsConcurrencyToken();
        builder.HasOne<Business>().WithMany().HasForeignKey(x => x.BusinessId).OnDelete(DeleteBehavior.Restrict);
    }
}
