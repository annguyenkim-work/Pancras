namespace Pancras.Application.Suppliers;

public interface ISupplierPurchaseStatsReader
{
    Task<IReadOnlyDictionary<Guid, SupplierPurchaseStats>> ReadAsync(
        IReadOnlyCollection<Guid> supplierIds,
        CancellationToken cancellationToken);
}
