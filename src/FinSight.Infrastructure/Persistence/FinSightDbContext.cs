using FinSight.Domain.Entities;
using FinSight.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;

namespace FinSight.Infrastructure.Persistence;

public sealed class FinSightDbContext(
    DbContextOptions<FinSightDbContext> options)
    : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>(options), IDataProtectionKeyContext
{
    public DbSet<DataProtectionKey> DataProtectionKeys { get; set; } = null!;

    public DbSet<Business> Businesses => Set<Business>();

    public DbSet<Product> Products => Set<Product>();
    public DbSet<BusinessCost> BusinessCosts => Set<BusinessCost>();

    public DbSet<PricingSimulation> PricingSimulations => Set<PricingSimulation>();

    public DbSet<WhatIfSimulation> WhatIfSimulations => Set<WhatIfSimulation>();

    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(FinSightDbContext).Assembly);
    }
}
