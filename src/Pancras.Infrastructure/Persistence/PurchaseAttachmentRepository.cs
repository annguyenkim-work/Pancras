using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pancras.Application.Purchasing;
using Pancras.Domain.Purchasing;

namespace Pancras.Infrastructure.Persistence;

public sealed class PurchaseAttachmentConfiguration : IEntityTypeConfiguration<PurchaseAttachment>
{
    public void Configure(EntityTypeBuilder<PurchaseAttachment> builder)
    {
        builder.ToTable("purchase_attachment");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Kind).HasConversion<int>().IsRequired();
        builder.Property(item => item.FileName).HasMaxLength(PurchaseAttachment.FileNameMaxLength).IsRequired();
        builder.Property(item => item.StoredName).HasMaxLength(40).IsRequired();
        builder.Property(item => item.ContentType).HasMaxLength(100).IsRequired();
        builder.Property(item => item.SizeBytes).IsRequired();
        builder.Property(item => item.UploadedAt).IsRequired();
        builder.Property(item => item.UploadedBy).HasMaxLength(PurchaseAttachment.UploadedByMaxLength).IsRequired();
        builder.HasIndex(item => item.PurchaseOrderId);
        builder.HasOne<PurchaseOrder>().WithMany().HasForeignKey(item => item.PurchaseOrderId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class PurchaseAttachmentRepository(PancrasDbContext context) : IPurchaseAttachmentRepository
{
    public async Task<IReadOnlyList<PurchaseAttachment>> ListAsync(Guid purchaseOrderId, CancellationToken cancellationToken) =>
        await context.PurchaseAttachments.AsNoTracking()
            .Where(item => item.PurchaseOrderId == purchaseOrderId)
            .ToListAsync(cancellationToken);

    public Task<PurchaseAttachment?> FindAsync(Guid id, CancellationToken cancellationToken) =>
        context.PurchaseAttachments.AsNoTracking().FirstOrDefaultAsync(item => item.Id == id, cancellationToken);

    public async Task AddAsync(PurchaseAttachment attachment, CancellationToken cancellationToken)
    {
        await context.PurchaseAttachments.AddAsync(attachment, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task RemoveAsync(Guid id, CancellationToken cancellationToken) =>
        await context.PurchaseAttachments.Where(item => item.Id == id).ExecuteDeleteAsync(cancellationToken);
}
