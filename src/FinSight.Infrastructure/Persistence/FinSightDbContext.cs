using FinSight.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace FinSight.Infrastructure.Persistence;

public sealed class FinSightDbContext(
    DbContextOptions<FinSightDbContext> options) : DbContext(options)
{
    public DbSet<Product> Products => Set<Product>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(FinSightDbContext).Assembly);

        base.OnModelCreating(modelBuilder);
    }
}
