using Pancras.Application.Purchasing;
using Pancras.Domain.Purchasing;

namespace Pancras.Application.Tests;

internal sealed class FakePurchaseStore : IPurchaseOrderRepository
{
    public List<PurchaseOrder> Orders { get; } = [];

    public Task<IReadOnlyList<PurchaseOrder>> ListAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<PurchaseOrder>>(Orders.ToArray());

    public Task<PurchaseOrder?> FindReadOnlyAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(Orders.FirstOrDefault(order => order.Id == id));

    public Task<PurchaseOrder?> FindForUpdateAsync(Guid id, CancellationToken cancellationToken) =>
        FindReadOnlyAsync(id, cancellationToken);

    public Task<bool> NormalizedCodeExistsAsync(string normalizedCode, CancellationToken cancellationToken) =>
        Task.FromResult(Orders.Any(order => order.NormalizedCode == normalizedCode));

    public Task<bool> TryAddAsync(PurchaseOrder order, CancellationToken cancellationToken)
    {
        if (Orders.Any(existing => existing.NormalizedCode == order.NormalizedCode))
        {
            return Task.FromResult(false);
        }

        Orders.Add(order);
        return Task.FromResult(true);
    }

    public Task<bool> CommitAsync(CancellationToken cancellationToken) => Task.FromResult(true);
}
