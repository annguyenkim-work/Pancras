using Microsoft.Extensions.Logging.Abstractions;
using Pancras.Application.Catalog;
using Pancras.Application.Common;
using Pancras.Application.Inventory;
using Pancras.Domain.Catalog;
using Pancras.Domain.Inventory;

namespace Pancras.Application.Tests;

public class ListStockBalancesHandlerTests
{
    [Fact]
    public async Task List_MatchesPartOfTheCodeOrName()
    {
        var store = new FakeInventoryStore();
        var location = StockLocation.Create("Kho A", StockLocationKind.Warehouse);
        store.Locations.Add(location);
        var lavabo = Item("ABC123", "Lavabo Toto", "Toto");
        var basin = Item("ZZ99", "Bồn cầu", "Inax");
        store.Items.Add(lavabo);
        store.Items.Add(basin);
        store.Balances.Add(StockBalance.Create(lavabo.Id, location.Id, 4, 0, 0));
        store.Balances.Add(StockBalance.Create(basin.Id, location.Id, 2, 0, 0));
        var handler = Handler(store);

        var byCode = await handler.HandleAsync(StockListFilter.From(null, null, "bc1"), TestActors.Admin, CancellationToken.None);
        var byName = await handler.HandleAsync(StockListFilter.From(null, null, "lav"), TestActors.Admin, CancellationToken.None);

        Assert.Equal("ABC123", Assert.Single(byCode).Code);
        Assert.Equal("ABC123", Assert.Single(byName).Code);
    }

    [Fact]
    public async Task List_KeepsOneLocation()
    {
        var store = new FakeInventoryStore();
        var warehouse = StockLocation.Create("Kho A", StockLocationKind.Warehouse);
        var showroom = StockLocation.Create("Showroom B", StockLocationKind.Showroom);
        store.Locations.Add(warehouse);
        store.Locations.Add(showroom);
        var item = Item("SP01", "Vòi", "Toto");
        store.Items.Add(item);
        store.Balances.Add(StockBalance.Create(item.Id, warehouse.Id, 5, 1, 2));
        store.Balances.Add(StockBalance.Create(item.Id, showroom.Id, 8, 0, 0));
        var handler = Handler(store);

        var rows = await handler.HandleAsync(StockListFilter.From(warehouse.Id.ToString("D"), null, null), TestActors.Admin, CancellationToken.None);

        var row = Assert.Single(rows);
        Assert.Equal(warehouse.Id, row.StockLocationId);
        Assert.Equal(4, row.Available);
        Assert.Equal(2, row.InTransit);
    }

    [Fact]
    public async Task List_KeepsOneCategory()
    {
        var store = new FakeInventoryStore();
        var location = StockLocation.Create("Kho A", StockLocationKind.Warehouse);
        store.Locations.Add(location);
        var sanitary = Item("SP01", "Vòi", "Toto", ProductCategory.SanitaryEquipment);
        var bathroom = Item("NT01", "Tủ", "Toto", ProductCategory.BathroomFurniture);
        store.Items.Add(sanitary);
        store.Items.Add(bathroom);
        store.Balances.Add(StockBalance.Create(sanitary.Id, location.Id, 1, 0, 0));
        store.Balances.Add(StockBalance.Create(bathroom.Id, location.Id, 1, 0, 0));
        var handler = Handler(store);

        var rows = await handler.HandleAsync(StockListFilter.From(null, ProductCategoryToken.Sanitary, null), TestActors.Admin, CancellationToken.None);

        Assert.Equal("SP01", Assert.Single(rows).Code);
    }

    [Fact]
    public async Task List_EmptySet_IsNotAnError()
    {
        var handler = Handler(new FakeInventoryStore());

        var rows = await handler.HandleAsync(StockListFilter.From(null, null, "không-có"), TestActors.Admin, CancellationToken.None);

        Assert.Empty(rows);
    }

    [Fact]
    public async Task Export_DoesNotCutTheFilteredSet()
    {
        var store = new FakeInventoryStore();
        var location = StockLocation.Create("Kho A", StockLocationKind.Warehouse);
        store.Locations.Add(location);
        for (var index = 1; index <= 25; index++)
        {
            var item = Item("SP" + index.ToString("00"), "Hàng " + index.ToString("00"), "Toto");
            store.Items.Add(item);
            store.Balances.Add(StockBalance.Create(item.Id, location.Id, index, 0, 0));
        }

        var rows = await Handler(store).ExportAsync(StockListFilter.From(null, null, null), TestActors.Admin, CancellationToken.None);

        Assert.Equal(25, rows.Count);
    }

    private static ListStockBalancesHandler Handler(FakeInventoryStore store) =>
        new(store, NullLogger<ListStockBalancesHandler>.Instance);

    private static StockItemInfo Item(
        string code,
        string name,
        string brand,
        ProductCategory category = ProductCategory.SanitaryEquipment) =>
        new(Guid.CreateVersion7(), code, name, brand, category);
}
