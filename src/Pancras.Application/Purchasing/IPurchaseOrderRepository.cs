using Pancras.Domain.Purchasing;

namespace Pancras.Application.Purchasing;

public interface IPurchaseOrderRepository
{
    Task<IReadOnlyList<PurchaseOrder>> ListAsync(CancellationToken cancellationToken);

    Task<PurchaseOrder?> FindReadOnlyAsync(Guid id, CancellationToken cancellationToken);

    Task<PurchaseOrder?> FindForUpdateAsync(Guid id, CancellationToken cancellationToken);

    Task<bool> NormalizedCodeExistsAsync(string normalizedCode, CancellationToken cancellationToken);

    Task AddAsync(PurchaseOrder order, CancellationToken cancellationToken);
}
