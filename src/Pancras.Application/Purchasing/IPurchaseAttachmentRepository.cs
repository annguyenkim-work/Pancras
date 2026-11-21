using Pancras.Domain.Purchasing;

namespace Pancras.Application.Purchasing;

public interface IPurchaseAttachmentRepository
{
    Task<IReadOnlyList<PurchaseAttachment>> ListAsync(Guid purchaseOrderId, CancellationToken cancellationToken);

    Task<PurchaseAttachment?> FindAsync(Guid id, CancellationToken cancellationToken);

    Task AddAsync(PurchaseAttachment attachment, CancellationToken cancellationToken);

    Task RemoveAsync(Guid id, CancellationToken cancellationToken);
}
