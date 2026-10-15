using FinSight.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System.Text.Json;
using FinSight.Domain.Models;

namespace FinSight.Infrastructure.Persistence.Configurations;

public sealed class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.HasKey(product => product.Id);

        builder.Property(product => product.Name)
            .IsRequired()
            .HasMaxLength(Product.MaxNameLength);

        builder.Property(product => product.SellingPrice)
            .HasPrecision(18, 2);

        builder.Property(product => product.UnitCost)
            .HasPrecision(18, 2).IsConcurrencyToken();
        builder.Property(product => product.CostBreakdown)
            .HasConversion(x => JsonSerializer.Serialize(x, (JsonSerializerOptions?)null),
                x => JsonSerializer.Deserialize<ProductCostBreakdown>(x, (JsonSerializerOptions?)null))
            .HasColumnType("text").IsConcurrencyToken();

        builder.Property(product => product.BusinessId)
            .IsRequired();

        builder.Property(product => product.IsActive)
            .IsRequired();

        builder.HasIndex(product => product.BusinessId);

        builder.HasOne<Business>()
            .WithMany()
            .HasForeignKey(product => product.BusinessId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
