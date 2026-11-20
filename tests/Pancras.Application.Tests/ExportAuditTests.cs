using Microsoft.Extensions.Logging.Abstractions;
using Pancras.Application.Catalog;
using Pancras.Application.Common;
using Pancras.Application.Inventory;
using Pancras.Domain.Auditing;
using Pancras.Domain.Catalog;

namespace Pancras.Application.Tests;

/// <summary>Xuất file ghi nhật ký (ai xuất, khi nào); xem trên màn hình thì không ghi.</summary>
public class ExportAuditTests
{
    [Fact]
    public async Task StockExport_IsAudited_ButAListIsNot()
    {
        var store = new FakeInventoryStore();
        var audit = new MemoryAudit();
        var handler = new ListStockBalancesHandler(store, NullLogger<ListStockBalancesHandler>.Instance, audit: audit);

        await handler.HandleAsync(ListStockBalancesQuery.From(null, null, null), TestActors.Admin, CancellationToken.None);
        Assert.Empty(audit.Entries);

        await handler.ExportAsync(ListStockBalancesQuery.From(null, null, null), TestActors.Admin, CancellationToken.None);

        var entry = Assert.Single(audit.Entries);
        Assert.Equal(AuditActions.StockExported, entry.Action);
        Assert.Equal(TestActors.Admin.Id, entry.ActorEmployeeId);
    }

    [Fact]
    public async Task CatalogExport_IsAudited_AndOnlyForRolesThatCanViewTheCatalog()
    {
        var repository = new FakeCatalogRepository();
        repository.Items.Add(ListCatalogItemsHandlerTests.Item("SP01", "Vòi", ProductCategory.SanitaryEquipment));
        var audit = new MemoryAudit();
        var handler = new ExportCatalogItemsHandler(repository, NullLogger<ExportCatalogItemsHandler>.Instance, audit: audit);

        var allowed = await handler.HandleAsync(ListCatalogItemsQuery.From(null, null), TestActors.Accountant, CancellationToken.None);
        var denied = await handler.HandleAsync(ListCatalogItemsQuery.From(null, null), TestActors.Keeper(Guid.NewGuid()), CancellationToken.None);

        Assert.True(allowed.IsSuccess);
        Assert.Single(allowed.Value!);
        Assert.Equal(CatalogError.Forbidden, denied.Error);
        Assert.Equal([AuditActions.CatalogExported, AuditActions.AccessDenied], audit.Entries.Select(entry => entry.Action));
    }
}
