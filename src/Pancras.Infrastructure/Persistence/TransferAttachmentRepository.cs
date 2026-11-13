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

    public async Task AddAsync(TransferAttachment attachment, CancellationToken cancellationToken)
    {
        await context.TransferAttachments.AddAsync(attachment, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
    }
}
