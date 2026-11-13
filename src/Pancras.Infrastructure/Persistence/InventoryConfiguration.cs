using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pancras.Domain.Catalog;
using Pancras.Domain.Inventory;

namespace Pancras.Infrastructure.Persistence;

public sealed class StockLocationConfiguration : IEntityTypeConfiguration<StockLocation>
{
    public void Configure(EntityTypeBuilder<StockLocation> builder)
    {
        builder.ToTable("stock_location");
        builder.HasKey(location => location.Id);
        builder.Property(location => location.Name).HasMaxLength(StockLocation.NameMaxLength).IsRequired();
        builder.Property(location => location.Kind).HasConversion<int>().IsRequired();
        builder.Property(location => location.IsDeleted).IsRequired();
        builder.HasQueryFilter(location => !location.IsDeleted);
        builder.HasIndex(location => location.Name).IsUnique().HasFilter("NOT is_deleted");
    }
}

public sealed class StockBalanceConfiguration : IEntityTypeConfiguration<StockBalance>
{
    public void Configure(EntityTypeBuilder<StockBalance> builder)
    {
        builder.ToTable("stock_balance");
        builder.HasKey(balance => balance.Id);
        builder.Property(balance => balance.CatalogItemId).IsRequired();
        builder.Property(balance => balance.StockLocationId).IsRequired();
        builder.Property(balance => balance.OnHand).IsRequired();
        builder.Property(balance => balance.Reserved).IsRequired();
        builder.Property(balance => balance.InTransit).IsRequired();
        builder.Property(balance => balance.Version).IsConcurrencyToken().IsRequired();
        builder.HasIndex(balance => new { balance.CatalogItemId, balance.StockLocationId }).IsUnique();
        builder.HasOne<CatalogItem>().WithMany().HasForeignKey(balance => balance.CatalogItemId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<StockLocation>().WithMany().HasForeignKey(balance => balance.StockLocationId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class StockMovementConfiguration : IEntityTypeConfiguration<StockMovement>
{
    public void Configure(EntityTypeBuilder<StockMovement> builder)
    {
        builder.ToTable("stock_movement");
        builder.HasKey(movement => movement.Id);
        builder.Property(movement => movement.CatalogItemId).IsRequired();
        builder.Property(movement => movement.StockLocationId).IsRequired();
        builder.Property(movement => movement.Kind).HasConversion<int>().IsRequired();
        builder.Property(movement => movement.Quantity).IsRequired();
        builder.Property(movement => movement.DocumentId).IsRequired();
        builder.Property(movement => movement.OccurredAt).IsRequired();
        builder.HasOne<CatalogItem>().WithMany().HasForeignKey(movement => movement.CatalogItemId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<StockLocation>().WithMany().HasForeignKey(movement => movement.StockLocationId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class TransferSlipConfiguration : IEntityTypeConfiguration<TransferSlip>
{
    public void Configure(EntityTypeBuilder<TransferSlip> builder)
    {
        builder.ToTable("transfer_slip");
        builder.HasKey(slip => slip.Id);
        builder.Property(slip => slip.SourceLocationId).IsRequired();
        builder.Property(slip => slip.DestinationLocationId).IsRequired();
        builder.Property(slip => slip.Note).HasMaxLength(TransferSlip.NoteMaxLength).IsRequired();
        builder.Property(slip => slip.Status).HasConversion<int>().IsRequired();
        builder.Property(slip => slip.SealNumber).HasMaxLength(TransferSlip.SealMaxLength).IsRequired();
        builder.Property(slip => slip.Carrier).HasMaxLength(TransferSlip.CarrierMaxLength).IsRequired();
        builder.Property(slip => slip.ReceiveNote).HasMaxLength(TransferSlip.ReasonMaxLength).IsRequired();
        builder.Property(slip => slip.ResolutionNote).HasMaxLength(TransferSlip.ReasonMaxLength).IsRequired();
        builder.Property(slip => slip.ApprovedBy).HasMaxLength(64).IsRequired();
        builder.Property(slip => slip.ShippedBy).HasMaxLength(64).IsRequired();
        builder.Property(slip => slip.ReceivedBy).HasMaxLength(64).IsRequired();
        builder.Property(slip => slip.ResolvedBy).HasMaxLength(64).IsRequired();
        builder.HasMany(slip => slip.Lines).WithOne().HasForeignKey(line => line.TransferSlipId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(slip => slip.Lines).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.HasOne<StockLocation>().WithMany().HasForeignKey(slip => slip.SourceLocationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<StockLocation>().WithMany().HasForeignKey(slip => slip.DestinationLocationId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class TransferLineConfiguration : IEntityTypeConfiguration<TransferLine>
{
    public void Configure(EntityTypeBuilder<TransferLine> builder)
    {
        builder.ToTable("transfer_line");
        builder.HasKey(line => line.Id);
        builder.Property(line => line.TransferSlipId).IsRequired();
        builder.Property(line => line.CatalogItemId).IsRequired();
        builder.Property(line => line.Id).ValueGeneratedNever();
        builder.Property(line => line.Quantity).IsRequired();
        builder.Property(line => line.ReceivedQuantity);
        builder.HasOne<CatalogItem>().WithMany().HasForeignKey(line => line.CatalogItemId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class StocktakeSheetConfiguration : IEntityTypeConfiguration<StocktakeSheet>
{
    public void Configure(EntityTypeBuilder<StocktakeSheet> builder)
    {
        builder.ToTable("stocktake_sheet");
        builder.HasKey(sheet => sheet.Id);
        builder.Property(sheet => sheet.StockLocationId).IsRequired();
        builder.Property(sheet => sheet.CatalogItemId).IsRequired();
        builder.Property(sheet => sheet.BookQuantity).IsRequired();
        builder.Property(sheet => sheet.CountedQuantity).IsRequired();
        builder.Property(sheet => sheet.Reason).HasMaxLength(StocktakeSheet.ReasonMaxLength).IsRequired();
        builder.Property(sheet => sheet.DocumentRef).HasMaxLength(StocktakeSheet.DocumentRefMaxLength).IsRequired();
        builder.Property(sheet => sheet.ImageRef).HasMaxLength(StocktakeSheet.ImageRefMaxLength).IsRequired();
        builder.Property(sheet => sheet.CreatedByRole).HasMaxLength(50).IsRequired();
        builder.Property(sheet => sheet.Status).HasConversion<int>().IsRequired();
        builder.Ignore(sheet => sheet.Variance);
        builder.HasOne<StockLocation>().WithMany().HasForeignKey(sheet => sheet.StockLocationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<CatalogItem>().WithMany().HasForeignKey(sheet => sheet.CatalogItemId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class QuantityAdjustmentConfiguration : IEntityTypeConfiguration<QuantityAdjustment>
{
    public void Configure(EntityTypeBuilder<QuantityAdjustment> builder)
    {
        builder.ToTable("quantity_adjustment");
        builder.HasKey(adjustment => adjustment.Id);
        builder.Property(adjustment => adjustment.StockLocationId).IsRequired();
        builder.Property(adjustment => adjustment.CatalogItemId).IsRequired();
        builder.Property(adjustment => adjustment.Delta).IsRequired();
        builder.Property(adjustment => adjustment.Reason).HasMaxLength(StocktakeSheet.ReasonMaxLength).IsRequired();
        builder.Property(adjustment => adjustment.DocumentRef).HasMaxLength(StocktakeSheet.DocumentRefMaxLength).IsRequired();
        builder.Property(adjustment => adjustment.ImageRef).HasMaxLength(StocktakeSheet.ImageRefMaxLength).IsRequired();
        builder.Property(adjustment => adjustment.Status).HasConversion<int>().IsRequired();
        builder.HasOne<StockLocation>().WithMany().HasForeignKey(adjustment => adjustment.StockLocationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<CatalogItem>().WithMany().HasForeignKey(adjustment => adjustment.CatalogItemId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class DisposalSlipConfiguration : IEntityTypeConfiguration<DisposalSlip>
{
    public void Configure(EntityTypeBuilder<DisposalSlip> builder)
    {
        builder.ToTable("disposal_slip");
        builder.HasKey(slip => slip.Id);
        builder.Property(slip => slip.StockLocationId).IsRequired();
        builder.Property(slip => slip.CatalogItemId).IsRequired();
        builder.Property(slip => slip.Reason).HasMaxLength(StocktakeSheet.ReasonMaxLength).IsRequired();
        builder.Property(slip => slip.DocumentRef).HasMaxLength(StocktakeSheet.DocumentRefMaxLength).IsRequired();
        builder.Property(slip => slip.ImageRef).HasMaxLength(StocktakeSheet.ImageRefMaxLength).IsRequired();
        builder.Property(slip => slip.CreatedByRole).HasMaxLength(50).IsRequired();
        builder.Property(slip => slip.Status).HasConversion<int>().IsRequired();
        builder.HasOne<StockLocation>().WithMany().HasForeignKey(slip => slip.StockLocationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<CatalogItem>().WithMany().HasForeignKey(slip => slip.CatalogItemId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class StockNoticeConfiguration : IEntityTypeConfiguration<StockNotice>
{
    public void Configure(EntityTypeBuilder<StockNotice> builder)
    {
        builder.ToTable("stock_notice");
        builder.HasKey(notice => notice.Id);
        builder.Property(notice => notice.RecipientEmployeeId).IsRequired();
        builder.Property(notice => notice.StockLocationId).IsRequired();
        builder.Property(notice => notice.DocumentId).IsRequired();
        builder.Property(notice => notice.Kind).HasConversion<int>().IsRequired();
        builder.Property(notice => notice.Step).HasMaxLength(120).IsRequired();
        builder.Property(notice => notice.Seen).IsRequired();
        builder.Property(notice => notice.CreatedAt).IsRequired();
        builder.HasOne<StockLocation>().WithMany().HasForeignKey(notice => notice.StockLocationId).OnDelete(DeleteBehavior.Restrict);
    }
}
