using Microsoft.Extensions.Logging;
using Pancras.Application.Auditing;
using Pancras.Application.Common;
using Pancras.Domain.Auditing;
using Pancras.Domain.Catalog;
using Pancras.Domain.Employees;

namespace Pancras.Application.Catalog;

/// <summary>Lấy danh mục để xuất file (có giá vốn): kiểm tra quyền xem danh mục và ghi nhật ký.</summary>
public sealed class ExportCatalogItemsHandler(
    ICatalogItemRepository repository,
    ILogger<ExportCatalogItemsHandler> logger,
    TimeProvider? clock = null,
    IAuditLog? audit = null,
    IRequestInfo? request = null)
{
    public async Task<Result<IReadOnlyList<CatalogItemRow>, CatalogError>> HandleAsync(
        ListCatalogItemsQuery filter,
        EmployeeActor actor,
        CancellationToken cancellationToken)
    {
        if (!CatalogAccess.CanView(actor.Role))
        {
            logger.LogWarning("Catalog export forbidden for role {Role}.", actor.Role);
            await audit.RecordAsync(clock ?? TimeProvider.System, request, actor.Id, null, AuditActions.AccessDenied, "catalog_item", null, "Xuất danh mục", cancellationToken);
            return Result<IReadOnlyList<CatalogItemRow>, CatalogError>.Fail(CatalogError.Forbidden);
        }

        IReadOnlyList<CatalogItemRow> rows = [];
        if (!filter.CategoryRejected)
        {
            rows = (await repository.ListMatchingAsync(filter, cancellationToken)).Select(CatalogItemRow.From).ToArray();
        }

        await audit.RecordAsync(clock ?? TimeProvider.System, request, actor.Id, null, AuditActions.CatalogExported, "catalog_item", null, rows.Count + " dòng, có giá vốn", cancellationToken);
        logger.LogInformation("Exported {CatalogItemCount} catalog items.", rows.Count);
        return Result<IReadOnlyList<CatalogItemRow>, CatalogError>.Ok(rows);
    }
}
