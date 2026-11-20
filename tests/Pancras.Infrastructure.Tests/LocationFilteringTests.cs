using Microsoft.EntityFrameworkCore;
using Pancras.Domain.Inventory;
using Pancras.Infrastructure.Persistence;

namespace Pancras.Infrastructure.Tests;

/// <summary>Kho/showroom Disable chỉ bị ẩn do từng truy vấn đọc lọc <c>IsActive</c>, không còn query filter toàn cục.</summary>
public class LocationFilteringTests(PostgresFixture fixture) : PostgresTest(fixture)
{
    private async Task<(StockLocation Active, StockLocation Disabled)> SeedAsync()
    {
        var active = StockLocation.Create("Kho A", StockLocationKind.Warehouse);
        var disabled = StockLocation.Create("Kho cũ", StockLocationKind.Warehouse);
        disabled.Deactivate();
        await using var context = new PancrasDbContext(Options);
        context.StockLocations.AddRange(active, disabled);
        await context.SaveChangesAsync();
        return (active, disabled);
    }

    [Fact]
    public async Task ListsHideDisabledLocations_ButTheDisabledListShowsThem()
    {
        var (active, disabled) = await SeedAsync();
        await using var context = new PancrasDbContext(Options);
        var locations = new LocationRepository(context);
        var inventory = new InventoryRepository(context, Microsoft.Extensions.Logging.Abstractions.NullLogger<InventoryRepository>.Instance);

        Assert.Equal([active.Id], (await locations.ListAsync(default)).Select(l => l.Id));
        Assert.Equal([active.Id], (await inventory.ListLocationsAsync(default)).Select(l => l.Id));
        Assert.Equal([disabled.Id], (await inventory.ListDeletedLocationsAsync(default)).Select(l => l.Id));
    }

    [Fact]
    public async Task ExistenceAndNameChecksIgnoreDisabledLocations()
    {
        var (active, disabled) = await SeedAsync();
        await using var context = new PancrasDbContext(Options);
        var locations = new LocationRepository(context);

        Assert.True(await locations.ExistsAsync(active.Id, default));
        Assert.False(await locations.ExistsAsync(disabled.Id, default));
        Assert.True(await locations.NameExistsAsync("kho a", default));
        Assert.False(await locations.NameExistsAsync("Kho cũ", default));
    }
}
