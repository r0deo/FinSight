using FinSight.Domain.Entities;
using FinSight.Domain.Models;
using System.Text.Json;
using FinSight.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinSight.Infrastructure.Persistence.Configurations;

public sealed class PricingSimulationConfiguration
    : IEntityTypeConfiguration<PricingSimulation>
{
    public void Configure(EntityTypeBuilder<PricingSimulation> builder)
    {
        builder.Property(x => x.CostContext)
            .HasConversion(x => JsonSerializer.Serialize(x, (JsonSerializerOptions?)null),
                x => JsonSerializer.Deserialize<SimulationCostContext>(x, (JsonSerializerOptions?)null)!)
            .HasColumnType("text");
        builder.Property(x => x.AdditionalDecisionCost).HasPrecision(18, 2);
        builder.HasKey(simulation => simulation.Id);

        builder.Property(simulation => simulation.CurrentPrice).HasPrecision(18, 2);
        builder.Property(simulation => simulation.UnitCost).HasPrecision(18, 2);
        builder.Property(simulation => simulation.ProposedPrice).HasPrecision(18, 2);
        builder.Property(simulation => simulation.CurrentContribution).HasPrecision(18, 2);
        builder.Property(simulation => simulation.ProposedContribution).HasPrecision(18, 2);
        builder.Property(simulation => simulation.Difference).HasPrecision(18, 2);
        builder.Property(simulation => simulation.ComparisonPeriod)
            .HasConversion<string>().HasMaxLength(16);
        builder.Property(simulation => simulation.DeclinePercentage);
        builder.Property(simulation => simulation.CurrencySnapshot).HasMaxLength(3);
        builder.Property(simulation => simulation.ProductNameSnapshot).HasMaxLength(Product.MaxNameLength);
        builder.Property(simulation => simulation.CalculationVersion).HasDefaultValue(0);
        builder.Property(x => x.DiscountSnapshot)
            .HasConversion(x => JsonSerializer.Serialize(x, (JsonSerializerOptions?)null),
                x => JsonSerializer.Deserialize<DiscountSimulationSnapshot>(x, (JsonSerializerOptions?)null)!)
            .HasColumnType("text");
        builder.Property(x => x.DeliverySnapshot)
            .HasConversion(x => JsonSerializer.Serialize(x, (JsonSerializerOptions?)null),
                x => JsonSerializer.Deserialize<DeliverySimulationSnapshot>(x, (JsonSerializerOptions?)null)!)
            .HasColumnType("text");
        builder.Property(simulation => simulation.Recommendation)
            .HasConversion<string>()
            .HasMaxLength(32);
        builder.Property(simulation => simulation.Version)
            .IsConcurrencyToken();
        builder.Ignore(simulation => simulation.IsArchived);
        builder.Ignore(simulation => simulation.IsDeleted);

        builder.HasIndex(simulation => new
        {
            simulation.BusinessId,
            simulation.CreatedAtUtc
        });

        builder.HasOne<Business>()
            .WithMany()
            .HasForeignKey(simulation => simulation.BusinessId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Product>()
            .WithMany()
            .HasForeignKey(simulation => simulation.ProductId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(simulation => simulation.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
