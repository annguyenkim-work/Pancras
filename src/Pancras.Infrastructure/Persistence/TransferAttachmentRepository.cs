using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pancras.Application.Inventory;
using Pancras.Domain.Inventory;

namespace Pancras.Infrastructure.Persistence;

public sealed class TransferAttachmentConfiguration : IEntityTypeConfiguration<TransferAttachment>
{
    public void Configure(EntityTypeBuilder<TransferAttachment> builder)
    {
        builder.ToTable("transfer_attachment");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.FileName).HasMaxLength(Pancras.Domain.Purchasing.PurchaseAttachment.FileNameMaxLength).IsRequired();
        builder.Property(item => item.StoredName).HasMaxLength(40).IsRequired();
        builder.Property(item => item.ContentType).HasMaxLength(100).IsRequired();
        builder.Property(item => item.SizeBytes).IsRequired();
        builder.Property(item => item.UploadedAt).IsRequired();
        builder.Property(item => item.UploadedBy).HasMaxLength(Pancras.Domain.Purchasing.PurchaseAttachment.UploadedByMaxLength).IsRequired();
        builder.HasIndex(item => item.TransferSlipId);
        builder.HasOne<TransferSlip>().WithMany().HasForeignKey(item => item.TransferSlipId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class TransferAttachmentRepository(PancrasDbContext context) : ITransferAttachmentRepository
{
    public async Task<IReadOnlyList<TransferAttachment>> ListAsync(Guid transferSlipId, CancellationToken cancellationToken) =>
        await context.TransferAttachments.AsNoTracking().Where(item => item.TransferSlipId == transferSlipId).ToListAsync(cancellationToken);

    public Task<TransferAttachment?> FindAsync(Guid id, CancellationToken cancellationToken) =>
        context.TransferAttachments.AsNoTracking().FirstOrDefaultAsync(item => item.Id == id, cancellationToken);

    public async Task<IReadOnlyDictionary<Guid, IReadOnlyList<TransferAttachment>>> ListForSlipsAsync(IReadOnlyCollection<Guid> transferSlipIds, CancellationToken cancellationToken)
    {
        var items = await context.TransferAttachments.AsNoTracking().Where(item => transferSlipIds.Contains(item.TransferSlipId)).ToListAsync(cancellationToken);
        return items.GroupBy(item => item.TransferSlipId).ToDictionary(group => group.Key, group => (IReadOnlyList<TransferAttachment>)group.ToList());
    }

    public Task AddAsync(TransferAttachment attachment, CancellationToken cancellationToken) =>
        context.TransferAttachments.AddAsync(attachment, cancellationToken).AsTask();
}

public sealed class StockDocumentAttachmentConfiguration : IEntityTypeConfiguration<StockDocumentAttachment>
{
    public void Configure(EntityTypeBuilder<StockDocumentAttachment> builder)
    {
        builder.ToTable("stock_document_attachment");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Kind).HasConversion<int>().IsRequired();
        builder.Property(item => item.DocumentId).IsRequired();
        builder.Property(item => item.FileName).HasMaxLength(Pancras.Domain.Purchasing.PurchaseAttachment.FileNameMaxLength).IsRequired();
        builder.Property(item => item.StoredName).HasMaxLength(40).IsRequired();
        builder.Property(item => item.ContentType).HasMaxLength(100).IsRequired();
        builder.Property(item => item.SizeBytes).IsRequired();
        builder.Property(item => item.UploadedAt).IsRequired();
        builder.Property(item => item.UploadedBy).HasMaxLength(Pancras.Domain.Purchasing.PurchaseAttachment.UploadedByMaxLength).IsRequired();
        builder.HasIndex(item => item.DocumentId);
    }
}

public sealed class StockDocumentAttachmentRepository(PancrasDbContext context) : IStockDocumentAttachmentRepository
{
    public async Task<IReadOnlyList<StockDocumentAttachment>> ListAsync(StockDocumentKind kind, Guid documentId, CancellationToken cancellationToken) =>
        await context.StockDocumentAttachments.AsNoTracking().Where(item => item.Kind == kind && item.DocumentId == documentId).ToListAsync(cancellationToken);

    public Task<StockDocumentAttachment?> FindAsync(Guid id, CancellationToken cancellationToken) =>
        context.StockDocumentAttachments.AsNoTracking().FirstOrDefaultAsync(item => item.Id == id, cancellationToken);

    public async Task<IReadOnlyDictionary<Guid, IReadOnlyList<StockDocumentAttachment>>> ListForDocumentsAsync(IReadOnlyCollection<Guid> documentIds, CancellationToken cancellationToken)
    {
        var items = await context.StockDocumentAttachments.AsNoTracking().Where(item => documentIds.Contains(item.DocumentId)).ToListAsync(cancellationToken);
        return items.GroupBy(item => item.DocumentId).ToDictionary(group => group.Key, group => (IReadOnlyList<StockDocumentAttachment>)group.ToList());
    }

    public Task AddAsync(StockDocumentAttachment attachment, CancellationToken cancellationToken) =>
        context.StockDocumentAttachments.AddAsync(attachment, cancellationToken).AsTask();
}
