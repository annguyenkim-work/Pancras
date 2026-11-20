using Pancras.Application.Common;
using Pancras.Application.Purchasing;
using Pancras.Domain.Purchasing;

namespace Pancras.Application.Tests;

internal sealed class FakePurchaseStore : IPurchaseOrderRepository, IUnitOfWork
{
    private readonly List<PurchaseOrder> _pending = [];

    public List<PurchaseOrder> Orders { get; } = [];

    public Task<IReadOnlyList<PurchaseOrder>> ListAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<PurchaseOrder>>(Orders.ToArray());

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
