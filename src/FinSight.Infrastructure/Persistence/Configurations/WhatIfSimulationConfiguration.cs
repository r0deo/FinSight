using System.Text.Json;
using FinSight.Domain.Entities;
using FinSight.Domain.Models;
using FinSight.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinSight.Infrastructure.Persistence.Configurations;

public sealed class WhatIfSimulationConfiguration : IEntityTypeConfiguration<WhatIfSimulation>
{
    public void Configure(EntityTypeBuilder<WhatIfSimulation> builder)
    {
        builder.Property(x => x.CostContext)
            .HasConversion(x => JsonSerializer.Serialize(x, (JsonSerializerOptions?)null),
                x => JsonSerializer.Deserialize<SimulationCostContext>(x, (JsonSerializerOptions?)null)!)
            .HasColumnType("text");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ProductNameSnapshot).HasMaxLength(Product.MaxNameLength).IsRequired();
        builder.Property(x => x.CurrencySnapshot).HasMaxLength(Business.CurrencyLength).IsRequired();
        builder.Property(x => x.Period).HasConversion<string>().HasMaxLength(16);
        // Immutable snapshots keep historical answers stable when products or formulas change.
        builder.Property(x => x.Inputs)
            .HasConversion(x => JsonSerializer.Serialize(x, (JsonSerializerOptions?)null),
                x => JsonSerializer.Deserialize<WhatIfInputs>(x, (JsonSerializerOptions?)null)!)
            .HasColumnType("text").IsRequired();
        builder.Property(x => x.Result)
            .HasConversion(x => JsonSerializer.Serialize(x, (JsonSerializerOptions?)null),
                x => JsonSerializer.Deserialize<WhatIfResult>(x, (JsonSerializerOptions?)null)!)
            .HasColumnType("text").IsRequired();
        builder.Property(x => x.Version).IsConcurrencyToken();
        builder.HasIndex(x => new { x.BusinessId, x.CreatedAtUtc });
        builder.HasOne<Business>().WithMany().HasForeignKey(x => x.BusinessId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Product>().WithMany().HasForeignKey(x => x.ProductId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
