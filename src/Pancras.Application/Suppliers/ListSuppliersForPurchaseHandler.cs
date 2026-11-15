using Microsoft.Extensions.Logging;
using Pancras.Domain.Employees;

namespace Pancras.Application.Suppliers;

/// <summary>Nhà cung cấp đang hợp tác, để chọn khi lập đơn mua. Chỉ người quản lý mua hàng.</summary>
public sealed class ListSuppliersForPurchaseHandler(
    ISupplierRepository repository,
    ILogger<ListSuppliersForPurchaseHandler> logger)
{
    public async Task<IReadOnlyList<SupplierPurchaseOption>> HandleAsync(EmployeeActor actor, CancellationToken cancellationToken)
    {
        if (!SupplierAccess.CanManage(actor.Role))
        {
            logger.LogWarning("Supplier choices forbidden for role {Role}.", actor.Role);
            return [];
        }

        var suppliers = await repository.ListAsync(cancellationToken);
        logger.LogInformation("Listed {SupplierCount} suppliers for purchase selection.", suppliers.Count);
        return suppliers
            .Select(supplier => new SupplierPurchaseOption(supplier.Id, supplier.Code, supplier.Name))
            .ToArray();
    }
}
