using Microsoft.Extensions.Logging;
using Pancras.Application.Common;
using Pancras.Domain.Employees;
using Pancras.Domain.Suppliers;

namespace Pancras.Application.Suppliers;

/// <summary>Hồ sơ đầy đủ của một nhà cung cấp (gồm tài khoản ngân hàng) để sửa hoặc xác nhận ngừng hợp tác. Chỉ người quản lý.</summary>
public sealed class GetSupplierProfileHandler(
    ISupplierRepository repository,
    ILogger<GetSupplierProfileHandler> logger,
    ISupplierPurchaseStatsReader? purchaseStats = null)
{
    public async Task<Result<SupplierProfile, SupplierError>> HandleAsync(Guid id, EmployeeActor actor, CancellationToken cancellationToken)
    {
        if (!SupplierAccess.CanManage(actor.Role))
        {
            logger.LogWarning("Supplier profile forbidden for role {Role}.", actor.Role);
            return Result<SupplierProfile, SupplierError>.Fail(SupplierError.Forbidden);
        }

        var supplier = await repository.GetAnyReadOnlyAsync(id, cancellationToken);
        if (supplier is null)
        {
            logger.LogInformation("Supplier profile was not found.");
            return Result<SupplierProfile, SupplierError>.Fail(SupplierError.NotFound);
        }

        var orders = 0;
        var open = 0;
        if (purchaseStats is not null)
        {
            var stats = await purchaseStats.ReadAsync([supplier.Id], cancellationToken);
            if (stats.TryGetValue(supplier.Id, out var row))
            {
                orders = row.PurchaseOrderCount;
                open = row.LotsNotReceived;
            }
        }

        return Result<SupplierProfile, SupplierError>.Ok(new SupplierProfile(
            supplier.Id,
            supplier.Code,
            supplier.Name,
            supplier.Country,
            supplier.ContactName,
            supplier.Phone,
            supplier.Email,
            supplier.BankName,
            supplier.Account,
            supplier.SwiftCode,
            orders,
            open,
            supplier.Version));
    }
}
