using Pancras.Domain.Suppliers;
using Microsoft.Extensions.Logging;
using Pancras.Application.Auditing;
using Pancras.Application.Common;
using Pancras.Domain.Auditing;
using Pancras.Domain.Employees;

namespace Pancras.Application.Suppliers;

/// <summary>Khôi phục nhà cung cấp đã ngừng hợp tác. Chỉ Admin.</summary>
public sealed class ReactivateSupplierCommandHandler(
    ISupplierRepository repository,
    ILogger<ReactivateSupplierCommandHandler> logger,
    TimeProvider? clock = null,
    IRequestInfo? request = null,
    IAuditLog? audit = null)
{
    public async Task<Result<Guid, SupplierError>> HandleAsync(Guid id, EmployeeActor actor, CancellationToken cancellationToken)
    {
        if (!SupplierAccess.CanReactivate(actor.Role))
        {
            logger.LogWarning("Supplier reactivation forbidden for role {Role}.", actor.Role);
            return Result<Guid, SupplierError>.Fail(SupplierError.Forbidden);
        }

        var supplier = await repository.GetAnyForUpdateAsync(id, cancellationToken);
        if (supplier is null)
        {
            logger.LogInformation("Supplier profile was not found.");
            return Result<Guid, SupplierError>.Fail(SupplierError.NotFound);
        }

        if (supplier.IsActive)
        {
            return Result<Guid, SupplierError>.Ok(supplier.Id);
        }

        // Trong lúc ngừng hợp tác, mã có thể đã được nhà cung cấp khác dùng.
        if (await repository.NormalizedCodeExistsAsync(supplier.NormalizedCode, supplier.Id, cancellationToken))
        {
            logger.LogWarning("Cannot reactivate supplier {SupplierId}: code is used.", supplier.Id);
            return CodeUsed();
        }

        supplier.Reactivate();
        if (!await repository.TrySaveAsync(cancellationToken))
        {
            return CodeUsed();
        }

        logger.LogInformation("Reactivated supplier {SupplierId}.", supplier.Id);
        await audit.RecordAsync(clock ?? TimeProvider.System, request, actor.Id, null, AuditActions.SupplierReactivated, "supplier", supplier.Id.ToString("D"), supplier.Code, cancellationToken);
        return Result<Guid, SupplierError>.Ok(supplier.Id);
    }

    private static Result<Guid, SupplierError> CodeUsed() =>
        Result<Guid, SupplierError>.Fail(SupplierError.DuplicateCode, [new SupplierIssue(SupplierField.Code, SupplierIssueKind.Duplicate)]);
}
