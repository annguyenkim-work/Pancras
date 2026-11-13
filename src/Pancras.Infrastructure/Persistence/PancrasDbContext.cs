using Microsoft.EntityFrameworkCore;
using Pancras.Domain.Catalog;
using Pancras.Domain.Employees;
using Pancras.Domain.Inventory;
using Pancras.Domain.Purchasing;
using Pancras.Domain.Suppliers;

namespace Pancras.Infrastructure.Persistence;

public sealed class PancrasDbContext(DbContextOptions<PancrasDbContext> options) : DbContext(options)
{
    public DbSet<Employee> Employees => Set<Employee>();

    public DbSet<Pancras.Domain.Auditing.AuditEntry> AuditEntries => Set<Pancras.Domain.Auditing.AuditEntry>();

    public DbSet<Supplier> Suppliers => Set<Supplier>();

    public DbSet<CatalogItem> CatalogItems => Set<CatalogItem>();

    public DbSet<StockLocation> StockLocations => Set<StockLocation>();

    public DbSet<StockBalance> StockBalances => Set<StockBalance>();

    public DbSet<StockMovement> StockMovements => Set<StockMovement>();

    public DbSet<TransferSlip> TransferSlips => Set<TransferSlip>();

    public DbSet<TransferLine> TransferLines => Set<TransferLine>();

    public DbSet<StocktakeSheet> StocktakeSheets => Set<StocktakeSheet>();

    public DbSet<QuantityAdjustment> QuantityAdjustments => Set<QuantityAdjustment>();

    public DbSet<DisposalSlip> DisposalSlips => Set<DisposalSlip>();

    public DbSet<StockNotice> StockNotices => Set<StockNotice>();

    public DbSet<PurchaseOrder> PurchaseOrders => Set<PurchaseOrder>();

    public DbSet<PurchaseLine> PurchaseLines => Set<PurchaseLine>();

    public DbSet<PurchaseCharge> PurchaseCharges => Set<PurchaseCharge>();

    public DbSet<PurchaseChargeShare> PurchaseChargeShares => Set<PurchaseChargeShare>();

    public DbSet<PurchaseAttachment> PurchaseAttachments => Set<PurchaseAttachment>();

    public DbSet<TransferAttachment> TransferAttachments => Set<TransferAttachment>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(PancrasDbContext).Assembly);

        // Dấu vết (ai tạo/sửa, lúc nào) có cùng cột và độ dài ở mọi thực thể kế thừa AuditStamped.
        foreach (var entityType in modelBuilder.Model.GetEntityTypes()
                     .Where(type => typeof(Pancras.Domain.Auditing.AuditStamped).IsAssignableFrom(type.ClrType))
                     .ToList())
        {
            var builder = modelBuilder.Entity(entityType.ClrType);
            builder.Property(nameof(Pancras.Domain.Auditing.AuditStamped.CreatedBy)).HasMaxLength(Pancras.Domain.Auditing.AuditStamped.ActorMaxLength).IsRequired();
            builder.Property(nameof(Pancras.Domain.Auditing.AuditStamped.UpdatedBy)).HasMaxLength(Pancras.Domain.Auditing.AuditStamped.ActorMaxLength).IsRequired();
        }
    }
}
