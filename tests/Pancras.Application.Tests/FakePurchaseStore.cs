using Pancras.Application.Common;
using Pancras.Application.Purchasing;
using Pancras.Domain.Purchasing;

namespace Pancras.Application.Tests;

internal sealed class FakePurchaseStore : IPurchaseOrderRepository, IUnitOfWork
{
    private readonly List<PurchaseOrder> _pending = [];

    public List<PurchaseOrder> Orders { get; } = [];

    public Task<IReadOnlyList<PurchaseShipmentKey>> ListShipmentKeysAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<PurchaseShipmentKey>>(Orders.Select(order => new PurchaseShipmentKey(order.Id, order.Code, order.ContainerNumber, order.BillOfLading, order.Status)).ToArray());

    public Task<PurchaseOrderPage> ListPageAsync(PurchaseOrderQuery filter, int pageNumber, int pageSize, CancellationToken cancellationToken)
    {
        var visible = Orders.Where(filter.Visible.Compile()).OrderByDescending(order => order.Id).ToList();
        var term = filter.Term?.Trim();
        var matching = visible
            .Where(order => filter.SupplierId is null || order.SupplierId == filter.SupplierId)
            .Where(order => string.IsNullOrEmpty(term)
                || new[] { order.Code, order.ContainerNumber, order.BillOfLading, order.Port, order.SupplierName }
                    .Any(text => text.Contains(term, StringComparison.OrdinalIgnoreCase)))
            .ToList();
        var size = Math.Max(1, pageSize);
        var page = Math.Clamp(pageNumber, 1, Math.Max(1, (matching.Count + size - 1) / size));
        return Task.FromResult(new PurchaseOrderPage(
            matching.Skip((page - 1) * size).Take(size).ToArray(),
            matching.Count,
            page,
            visible.Count > 0,
            matching.Count == 0 ? null : matching[0].Id));
    }

    public Task<PurchaseOrder?> FindReadOnlyAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(Orders.FirstOrDefault(order => order.Id == id));

    public Task<PurchaseOrder?> FindForUpdateAsync(Guid id, CancellationToken cancellationToken) =>
        FindReadOnlyAsync(id, cancellationToken);

    public Task<bool> NormalizedCodeExistsAsync(string normalizedCode, CancellationToken cancellationToken) =>
        Task.FromResult(Orders.Any(order => order.NormalizedCode == normalizedCode));

    public Task AddAsync(PurchaseOrder order, CancellationToken cancellationToken)
    {
        _pending.Add(order);
        return Task.CompletedTask;
    }

    /// <summary>Kết quả của lần commit kế tiếp (một lần), để giả lập có người lưu chồng lên.</summary>
    public SaveOutcome? NextOutcome { get; set; }

    public Task<IUnitOfWorkScope> BeginAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IUnitOfWorkScope>(new FakeUnitOfWorkScope(Commit));

    private SaveOutcome Commit()
    {
        var staged = _pending.ToArray();
        _pending.Clear();
        if (NextOutcome is { } outcome)
        {
            NextOutcome = null;
            return outcome;
        }

        if (staged.Any(order => Orders.Any(existing => existing.NormalizedCode == order.NormalizedCode)))
        {
            return SaveOutcome.Duplicate;
        }

        Orders.AddRange(staged);
        return SaveOutcome.Saved;
    }
}
