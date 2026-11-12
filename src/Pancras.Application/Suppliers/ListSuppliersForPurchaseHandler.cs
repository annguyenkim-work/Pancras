using Microsoft.Extensions.Logging;

namespace Pancras.Application.Suppliers;

public sealed class ListSuppliersForPurchaseHandler(
    ISupplierRepository repository,
    ILogger<ListSuppliersForPurchaseHandler> logger)
{
    public async Task<IReadOnlyList<SupplierPurchaseOption>> HandleAsync(CancellationToken cancellationToken)
    {
        var suppliers = await repository.ListAsync(cancellationToken);
        logger.LogInformation("Listed {SupplierCount} suppliers for purchase selection.", suppliers.Count);
        return suppliers
            .Select(supplier => new SupplierPurchaseOption(supplier.Id, supplier.Code, supplier.Name))
            .ToArray();
    }
}
