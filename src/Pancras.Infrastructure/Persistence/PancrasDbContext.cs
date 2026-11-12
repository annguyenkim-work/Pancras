using Microsoft.EntityFrameworkCore;
using Pancras.Domain.Catalog;
using Pancras.Domain.Employees;
using Pancras.Domain.Suppliers;

namespace Pancras.Infrastructure.Persistence;

public sealed class PancrasDbContext(DbContextOptions<PancrasDbContext> options) : DbContext(options)
{
    public DbSet<Employee> Employees => Set<Employee>();

    public DbSet<Supplier> Suppliers => Set<Supplier>();

    public DbSet<CatalogItem> CatalogItems => Set<CatalogItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(PancrasDbContext).Assembly);
    }
}
