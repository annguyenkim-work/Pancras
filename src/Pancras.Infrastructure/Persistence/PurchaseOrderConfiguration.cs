using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pancras.Domain.Catalog;
using Pancras.Domain.Inventory;
using Pancras.Domain.Purchasing;
using Pancras.Domain.Suppliers;

namespace Pancras.Infrastructure.Persistence;

public sealed class PurchaseOrderConfiguration : IEntityTypeConfiguration<PurchaseOrder>
{
    public void Configure(EntityTypeBuilder<PurchaseOrder> builder)
    {
        builder.ToTable("purchase_order");
        builder.HasKey(order => order.Id);
        builder.Property(order => order.Code).HasMaxLength(PurchaseOrder.CodeMaxLength).IsRequired();
        builder.Property(order => order.NormalizedCode).HasMaxLength(PurchaseOrder.CodeMaxLength).IsRequired();
        builder.Property(order => order.ContainerNumber).HasMaxLength(PurchaseOrder.ContainerMaxLength).IsRequired();
        builder.Property(order => order.BillOfLading).HasMaxLength(PurchaseOrder.ContainerMaxLength).IsRequired();
        builder.Property(order => order.Port).HasMaxLength(PurchaseOrder.PortMaxLength).IsRequired();
        builder.Property(order => order.ExchangeRate).HasColumnType("numeric(18,0)").IsRequired();
        builder.Property(order => order.VatRate).HasColumnType("numeric(9,4)").IsRequired();
        builder.Property(order => order.Status).HasConversion<int>().IsRequired();
        builder.Property(order => order.VatDeductible).IsRequired();
        builder.HasIndex(order => order.NormalizedCode).IsUnique();
        builder.HasMany(order => order.Lines).WithOne().HasForeignKey(line => line.PurchaseOrderId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(order => order.Lines).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.HasMany(order => order.Charges).WithOne().HasForeignKey(charge => charge.PurchaseOrderId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(order => order.Charges).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.HasOne<Supplier>().WithMany().HasForeignKey(order => order.SupplierId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<StockLocation>().WithMany().HasForeignKey(order => order.ReceiveLocationId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class PurchaseLineConfiguration : IEntityTypeConfiguration<PurchaseLine>
{
    public void Configure(EntityTypeBuilder<PurchaseLine> builder)
    {
        builder.ToTable("purchase_line");
        builder.HasKey(line => line.Id);
        builder.Property(line => line.PurchaseOrderId).IsRequired();
        builder.Property(line => line.CatalogItemId).IsRequired();
        builder.Property(line => line.Quantity).IsRequired();
        builder.Property(line => line.FobUsd).HasColumnType("numeric(18,2)").IsRequired();
        builder.Property(line => line.DutyRate).HasColumnType("numeric(9,4)").IsRequired();
        builder.Property(line => line.SupplementalDuty).HasColumnType("numeric(18,0)").IsRequired();
        builder.Property(line => line.SpecialConsumptionTax).HasColumnType("numeric(18,0)").IsRequired();
        builder.Property(line => line.EnvironmentTax).HasColumnType("numeric(18,0)").IsRequired();
        builder.Property(line => line.DeclaredCustomsValue).HasColumnType("numeric(18,0)");
        builder.Property(line => line.DeclaredDuty).HasColumnType("numeric(18,0)");
        builder.Property(line => line.DeclaredVat).HasColumnType("numeric(18,0)");
        builder.HasOne<CatalogItem>().WithMany().HasForeignKey(line => line.CatalogItemId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class PurchaseChargeConfiguration : IEntityTypeConfiguration<PurchaseCharge>
{
    public void Configure(EntityTypeBuilder<PurchaseCharge> builder)
    {
        builder.ToTable("purchase_charge");
        builder.HasKey(charge => charge.Id);
        builder.Property(charge => charge.PurchaseOrderId).IsRequired();
        builder.Property(charge => charge.Amount).HasColumnType("numeric(18,0)").IsRequired();
        builder.Property(charge => charge.Method).HasConversion<int>().IsRequired();
        builder.Property(charge => charge.Group).HasColumnName("charge_group").HasConversion<int>();
        builder.Property(charge => charge.Label).HasMaxLength(PurchaseCharge.LabelMaxLength).IsRequired();
        builder.HasMany(charge => charge.Shares).WithOne().HasForeignKey(share => share.PurchaseChargeId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(charge => charge.Shares).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

public sealed class PurchaseChargeShareConfiguration : IEntityTypeConfiguration<PurchaseChargeShare>
{
    public void Configure(EntityTypeBuilder<PurchaseChargeShare> builder)
    {
        builder.ToTable("purchase_charge_share");
        builder.HasKey(share => share.Id);
        builder.Property(share => share.PurchaseChargeId).IsRequired();
        builder.Property(share => share.LineId).IsRequired();
        builder.Property(share => share.Amount).HasColumnType("numeric(18,0)").IsRequired();
    }
}
