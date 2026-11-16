using Microsoft.Extensions.Logging.Abstractions;
using Pancras.Application.Common;
using Pancras.Application.Suppliers;
using Pancras.Domain.Auditing;
using Pancras.Domain.Suppliers;

namespace Pancras.Application.Tests;

public class DeactivateSupplierCommandHandlerTests
{
    private readonly FakeSupplierRepository _repository = new();
    private readonly MemoryAudit _audit = new();

    [Fact]
    public async Task Deactivate_HidesTheSupplierFromPurchaseChoices_AndKeepsTheRow()
    {
        var supplier = AddSample();

        var result = await Deactivate().HandleAsync(supplier.Id, SupplierActors.Accountant, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.False(supplier.IsActive);
        Assert.Equal("NCC01", supplier.Code);
        Assert.Empty(await _repository.ListAsync(CancellationToken.None));

        var options = await new ListSuppliersForPurchaseHandler(_repository, NullLogger<ListSuppliersForPurchaseHandler>.Instance)
            .HandleAsync(SupplierActors.Accountant, CancellationToken.None);
        Assert.Empty(options);
        Assert.Contains(_audit.Entries, entry => entry.Action == AuditActions.SupplierDeactivated);
    }

    [Fact]
    public async Task OnlyManagers_CanDeactivate()
    {
        var supplier = AddSample();

        var keeper = await Deactivate().HandleAsync(supplier.Id, SupplierActors.Keeper, CancellationToken.None);
        var sales = await Deactivate().HandleAsync(supplier.Id, SupplierActors.Sales, CancellationToken.None);

        Assert.Equal(SupplierError.Forbidden, keeper.SupplierError);
        Assert.Equal(SupplierError.Forbidden, sales.SupplierError);
        Assert.True(supplier.IsActive);
    }

    [Fact]
    public async Task AnInactiveSupplier_StillAppearsUnderTheInactiveFilter()
    {
        var supplier = AddSample();
        supplier.Deactivate();

        var cards = await new ListSupplierCardsHandler(_repository, new FakeSupplierPurchaseStatsReader(), NullLogger<ListSupplierCardsHandler>.Instance)
            .HandleAsync(SupplierListFilter.From(null, SupplierStatusFilter.Inactive), SupplierPageRequest.First, SupplierActors.Admin, CancellationToken.None);

        var card = Assert.Single(cards.Cards);
        Assert.False(card.IsActive);
    }

    [Fact]
    public async Task ACodeHeldByAnInactiveSupplier_IsNotReusedSilently()
    {
        var supplier = AddSample();
        supplier.Deactivate();

        var result = await new CreateSupplierCommandHandler(_repository, NullLogger<CreateSupplierCommandHandler>.Instance)
            .HandleAsync(new CreateSupplierCommand
            {
                Actor = SupplierActors.Admin,
                Code = "ncc01",
                Name = "Kim An mới",
                Country = "Trung Quốc",
                ContactName = "Nguyễn An"
            }, CancellationToken.None);

        Assert.Equal(SupplierError.DuplicateCode, result.SupplierError);
        Assert.Contains(result.SupplierIssues, issue => issue.Field == SupplierField.Code && issue.Kind == SupplierIssueKind.DuplicateInactive);
        Assert.Single(_repository.Suppliers);
    }

    [Fact]
    public async Task ASupplierWithAnUnreceivedPurchaseOrder_CannotBeDeactivated()
    {
        var supplier = AddSample();
        var stats = new FakeSupplierPurchaseStatsReader();
        stats.Stats[supplier.Id] = new SupplierPurchaseStats(3, 1);

        var result = await Deactivate(stats).HandleAsync(supplier.Id, SupplierActors.Admin, CancellationToken.None);

        Assert.Equal(SupplierError.HasOpenPurchases, result.SupplierError);
        Assert.True(supplier.IsActive);
    }

    [Fact]
    public async Task ASupplierWithOnlyReceivedOrders_CanBeDeactivated_AndTheOrdersStillShowItsName()
    {
        var supplier = AddSample();
        var stats = new FakeSupplierPurchaseStatsReader();
        stats.Stats[supplier.Id] = new SupplierPurchaseStats(2, 0);
        var orders = new FakePurchaseStore();
        var order = Pancras.Domain.Purchasing.PurchaseOrder.TryCreate("PO-1", supplier.Id, Guid.CreateVersion7(), null, null, null, null, 25000m, 0m, [(Guid.CreateVersion7(), 1, 10m, 0m, 0m, 0m, 0m)])!;
        order.RecordSupplier(supplier.Code, supplier.Name);
        orders.Orders.Add(order);

        var result = await Deactivate(stats).HandleAsync(supplier.Id, SupplierActors.Admin, CancellationToken.None);
        var list = await new Pancras.Application.Purchasing.ListPurchaseOrdersHandler(
                orders,
                _repository,
                new FakeInventoryStore(),
                NullLogger<Pancras.Application.Purchasing.ListPurchaseOrdersHandler>.Instance)
            .HandleAsync(SupplierActors.Admin, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("Kim An", Assert.Single(list).SupplierName);
    }

    [Fact]
    public async Task MissingSupplier_IsNotFound()
    {
        var result = await Deactivate().HandleAsync(Guid.CreateVersion7(), SupplierActors.Admin, CancellationToken.None);

        Assert.Equal(SupplierError.NotFound, result.SupplierError);
    }

    [Fact]
    public async Task OnlyAdmin_CanReactivate()
    {
        var supplier = AddSample();
        supplier.Deactivate();

        var accountant = await Reactivate().HandleAsync(supplier.Id, SupplierActors.Accountant, CancellationToken.None);
        var director = await Reactivate().HandleAsync(supplier.Id, SupplierActors.Director, CancellationToken.None);
        Assert.Equal(SupplierError.Forbidden, accountant.SupplierError);
        Assert.Equal(SupplierError.Forbidden, director.SupplierError);
        Assert.False(supplier.IsActive);

        var admin = await Reactivate().HandleAsync(supplier.Id, SupplierActors.Admin, CancellationToken.None);
        Assert.True(admin.IsSuccess);
        Assert.True(supplier.IsActive);
        Assert.Contains(_audit.Entries, entry => entry.Action == AuditActions.SupplierReactivated);
    }

    [Fact]
    public async Task Reactivate_IsRefused_WhenAnotherActiveSupplierTookTheCode()
    {
        var supplier = AddSample();
        supplier.Deactivate();
        _repository.Suppliers.Add(Supplier.Create("NCC01", "Nhà khác", "Ý", "Lan", "", "", ""));

        var result = await Reactivate().HandleAsync(supplier.Id, SupplierActors.Admin, CancellationToken.None);

        Assert.Equal(SupplierError.DuplicateCode, result.SupplierError);
        Assert.False(supplier.IsActive);
    }

    private DeactivateSupplierCommandHandler Deactivate(ISupplierPurchaseStatsReader? stats = null) =>
        new(_repository, NullLogger<DeactivateSupplierCommandHandler>.Instance, stats, audit: _audit);

    private ReactivateSupplierCommandHandler Reactivate() =>
        new(_repository, NullLogger<ReactivateSupplierCommandHandler>.Instance, audit: _audit);

    private Supplier AddSample()
    {
        var supplier = Supplier.Create("NCC01", "Kim An", "Trung Quốc", "Nguyễn An", "0901000111", "a@b.co", "123456");
        _repository.Suppliers.Add(supplier);
        return supplier;
    }
}
