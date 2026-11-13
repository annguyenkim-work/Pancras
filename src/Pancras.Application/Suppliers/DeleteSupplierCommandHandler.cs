using Microsoft.Extensions.Logging;
using Pancras.Application.Common;

namespace Pancras.Application.Suppliers;

public sealed class DeleteSupplierCommandHandler(
    ISupplierRepository repository,
    ILogger<DeleteSupplierCommandHandler> logger,
    ISupplierPurchaseStatsReader? purchaseStats = null)
{
    public async Task<Result<Guid>> HandleAsync(Guid id, CancellationToken cancellationToken)
    {
        var supplier = await repository.GetActiveForUpdateAsync(id, cancellationToken);
        if (supplier is null)
        {
            logger.LogInformation("Supplier profile was not found.");
            return Result<Guid>.Fail(SupplierError.NotFound);
        }

        // Xóa mềm làm nhà cung cấp biến mất khỏi danh sách, nên không cho xóa khi còn đơn mua chưa nhập kho.
        if (purchaseStats is not null)
        {
            var stats = await purchaseStats.ReadAsync([supplier.Id], cancellationToken);
            if (stats.TryGetValue(supplier.Id, out var row) && row.LotsNotReceived > 0)
            {
                logger.LogWarning("Supplier {SupplierId} still has {OpenOrders} open purchase orders.", supplier.Id, row.LotsNotReceived);
                return Result<Guid>.Fail(SupplierError.HasOpenPurchases);
            }
        }

        supplier.MarkDeleted();
        if (!await repository.TrySaveAsync(cancellationToken))
        {
            logger.LogWarning("Could not save a supplier deletion for {SupplierId}.", supplier.Id);
            return Result<Guid>.Fail(SupplierError.NotFound);
        }

        logger.LogInformation("Soft-deleted supplier {SupplierId}.", supplier.Id);
        return Result<Guid>.Ok(supplier.Id);
    }
}
