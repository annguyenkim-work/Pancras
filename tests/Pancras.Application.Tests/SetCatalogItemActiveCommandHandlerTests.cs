using Microsoft.Extensions.Logging.Abstractions;
using Pancras.Application.Catalog;
using Pancras.Application.Common;
using Pancras.Domain.Catalog;
using Pancras.Domain.Employees;

namespace Pancras.Application.Tests;

public class SetCatalogItemActiveCommandHandlerTests
{
    private readonly FakeCatalogRepository _repository = new();

    private SetCatalogItemActiveCommandHandler Handler() =>
        new(_repository, NullLogger<SetCatalogItemActiveCommandHandler>.Instance);

    private CatalogItem AddItem()
    {
        var item = ListCatalogItemsHandlerTests.Item("SP01", "Vòi", ProductCategory.SanitaryEquipment);
        _repository.Items.Add(item);
        return item;
    }

    private static EmployeeActor Actor(string role) => new(Guid.NewGuid(), role, null);

    [Fact]
    public async Task Disable_ThenActive_TogglesTheFlag_AndAdvancesTheVersion()
    {
        var item = AddItem();

        var disabled = await Handler().HandleAsync(item.Id, false, item.Version, Actor(EmployeeRole.Admin), CancellationToken.None);
        Assert.Equal(CatalogError.None, disabled);
        Assert.False(item.IsActive);

        var activated = await Handler().HandleAsync(item.Id, true, item.Version, Actor(EmployeeRole.Accountant), CancellationToken.None);
        Assert.Equal(CatalogError.None, activated);
        Assert.True(item.IsActive);
    }

    [Theory]
    [InlineData(EmployeeRole.Sales)]
    [InlineData(EmployeeRole.ShowroomWarehouseLead)]
    [InlineData(EmployeeRole.WarehouseKeeper)]
    public async Task OtherRoles_CannotDisable(string role)
    {
        var item = AddItem();

        var result = await Handler().HandleAsync(item.Id, false, item.Version, Actor(role), CancellationToken.None);

        Assert.Equal(CatalogError.Forbidden, result);
        Assert.True(item.IsActive);
    }

    [Fact]
    public async Task StaleVersion_IsRejected_AndChangesNothing()
    {
        var item = AddItem();

        var result = await Handler().HandleAsync(item.Id, false, item.Version + 1, Actor(EmployeeRole.Admin), CancellationToken.None);

        Assert.Equal(CatalogError.VersionConflict, result);
        Assert.True(item.IsActive);
    }

    [Fact]
    public async Task DisabledItems_AreHiddenFromTheDefaultList_ButShownByTheDisabledAndAllFilters()
    {
        var item = AddItem();
        item.Deactivate();
        var list = new ListMatchingCatalogItemsHandler(_repository, NullLogger<ListMatchingCatalogItemsHandler>.Instance);

        var active = await list.HandleAsync(CatalogListFilter.From(null, null), CancellationToken.None);
        var disabled = await list.HandleAsync(CatalogListFilter.From(null, null, "disabled"), CancellationToken.None);
        var all = await list.HandleAsync(CatalogListFilter.From(null, null, "all"), CancellationToken.None);

        Assert.Empty(active);
        Assert.Single(disabled);
        Assert.Single(all);
    }
}
