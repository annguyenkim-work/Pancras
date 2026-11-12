using Microsoft.Extensions.Logging;
using Pancras.Application.Suppliers;

namespace Pancras.Infrastructure.Persistence;

public sealed class EmptySupplierPurchaseStatsReader(ILogger<EmptySupplierPurchaseStatsReader> logger)
    : ISupplierPurchaseStatsReader
{
    public Task<IReadOnlyDictionary<Guid, SupplierPurchaseStats>> ReadAsync(
        IReadOnlyCollection<Guid> supplierIds,
        CancellationToken cancellationToken)
    {
        logger.LogDebug("Purchase order stats are not available yet for {SupplierCount} suppliers.", supplierIds.Count);
        IReadOnlyDictionary<Guid, SupplierPurchaseStats> empty = new Dictionary<Guid, SupplierPurchaseStats>();
        return Task.FromResult(empty);
    }
}
