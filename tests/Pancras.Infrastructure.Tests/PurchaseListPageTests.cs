using Microsoft.EntityFrameworkCore;
using Pancras.Application.Common;
using Pancras.Application.Purchasing;
using Pancras.Domain.Employees;
using Pancras.Domain.Purchasing;
using Pancras.Infrastructure.Persistence;

namespace Pancras.Infrastructure.Tests;

/// <summary>Danh sách đơn mua cắt trang, lọc, đếm trong PostgreSQL; quyền xem do <see cref="PurchaseAccess.Visible"/>.</summary>
public class PurchaseListPageTests(PostgresFixture fixture) : PostgresTest(fixture)
{
    private static readonly EmployeeActor Manager = new(Guid.CreateVersion7(), EmployeeRole.Accountant, null);

    private Guid _supplierA;
    private Guid _supplierB;
    private Guid _here;
    private Guid _there;
    private Guid _item;

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        _supplierA = await SeedSupplierAsync();
        _supplierB = await SeedSupplierAsync();
        _here = await SeedLocationAsync("Kho A");
        _there = await SeedLocationAsync("Kho B");
        _item = (await SeedItemsAsync(1))[0];
    }

    private async Task<PurchaseOrder> AddOrderAsync(
        string code,
        Guid supplier,
        Guid location,
        string? container = null,
        string? billOfLading = null,
        string? port = null,
        string? supplierName = null,
        PurchaseStatus status = PurchaseStatus.Draft)
    {
        var order = PurchaseOrder.TryCreate(code, supplier, location, container, billOfLading, port, null, 25000m, 0m, [(_item, 2, 10m, 0m, 0m, 0m, 0m)])!;
        if (supplierName is not null)
        {
            order.RecordSupplier("S", supplierName);
        }

        if (status != PurchaseStatus.Draft)
        {
            Assert.True(order.TrySubmit());
            Assert.True(order.TryApprove());
        }

        if (status == PurchaseStatus.InTransit)
        {
            Assert.True(order.TryMarkInTransit());
        }

        await using var context = Database.NewContext();
        await new PurchaseOrderRepository(context).AddAsync(order, CancellationToken.None);
        Assert.Equal(SaveOutcome.Saved, await CommitAsync(context));
        return order;
    }

    private Task<PurchaseOrderPage> ListAsync(EmployeeActor actor, Guid? supplier = null, string? term = null, int page = 1, int size = 20) =>
        PageAsync(actor, supplier, term, null, page, size);

    private async Task<PurchaseOrderPage> PageAsync(EmployeeActor actor, Guid? supplier = null, string? term = null, PurchaseStatus? status = null, int page = 1, int size = 20)
    {
        await using var context = Database.NewContext();
        return await new PurchaseOrderRepository(context).ListPageAsync(
            new PurchaseOrderQuery(PurchaseAccess.Visible(actor), supplier, term, status), page, size, CancellationToken.None);
    }

    [Fact]
    public async Task ListPage_ReturnsNewestFirst_WithTheTotalAndOnlyThePageRows()
    {
        var created = new List<Guid>();
        for (var i = 1; i <= 5; i++)
        {
            created.Add((await AddOrderAsync("PO-" + i, _supplierA, _here)).Id);
        }

        var first = await ListAsync(Manager, size: 2);
        var last = await ListAsync(Manager, page: 3, size: 2);
        var beyond = await ListAsync(Manager, page: 9, size: 2);

        Assert.Equal(5, first.Total);
        Assert.Equal([created[4], created[3]], first.Items.Select(order => order.Id).ToArray());
        Assert.Equal(created[4], first.FirstId);
        Assert.Equal([created[0]], last.Items.Select(order => order.Id).ToArray());
        Assert.Equal(3, beyond.PageNumber);
        Assert.Equal(created[0], beyond.Items.Single().Id);
        Assert.All(first.Items, order => Assert.Single(order.Lines));
    }

    [Fact]
    public async Task ListPage_FiltersBySupplier_AndByEveryTextField_IgnoringCase()
    {
        await AddOrderAsync("PO-ONE", _supplierA, _here, container: "MSKU1234567", billOfLading: "BL-777", port: "Hải Phòng", supplierName: "Toto Vietnam");
        await AddOrderAsync("PO-TWO", _supplierB, _here, container: "TGHU7654321", billOfLading: "BL-888", port: "Cát Lái", supplierName: "Grohe");

        Assert.Equal("PO-ONE", Assert.Single((await ListAsync(Manager, supplier: _supplierA)).Items).Code);
        Assert.Equal("PO-TWO", Assert.Single((await ListAsync(Manager, term: "po-two")).Items).Code);
        Assert.Equal("PO-ONE", Assert.Single((await ListAsync(Manager, term: "msku123")).Items).Code);
        Assert.Equal("PO-TWO", Assert.Single((await ListAsync(Manager, term: "bl-888")).Items).Code);
        Assert.Equal("PO-ONE", Assert.Single((await ListAsync(Manager, term: "hải phòng")).Items).Code);
        Assert.Equal("PO-TWO", Assert.Single((await ListAsync(Manager, term: "grohe")).Items).Code);
        Assert.Empty((await ListAsync(Manager, term: "không-có")).Items);
    }

    [Fact]
    public async Task ListPage_TreatsLikeWildcardsInTheTermAsPlainText()
    {
        await AddOrderAsync("PO-100", _supplierA, _here);

        Assert.Empty((await ListAsync(Manager, term: "%")).Items);
        Assert.Empty((await ListAsync(Manager, term: "_")).Items);
        Assert.Single((await ListAsync(Manager, term: "PO-1")).Items);
    }

    [Fact]
    public async Task ListPage_SearchesTheCurrentSupplierName_WhenTheOrderHasNoSnapshot()
    {
        await using (var edit = Database.NewContext())
        {
            var supplier = await edit.Suppliers.SingleAsync(item => item.Id == _supplierA);
            supplier.Update(supplier.Code, "Nhà cung cấp Đặc Biệt", supplier.Country, supplier.ContactName, supplier.Phone, supplier.Email, supplier.Account, supplier.BankName, supplier.SwiftCode);
            await edit.SaveChangesAsync();
        }

        await AddOrderAsync("PO-NOSNAP", _supplierA, _here);
        await AddOrderAsync("PO-SNAP", _supplierB, _here, supplierName: "Tên đã chụp");

        Assert.Equal("PO-NOSNAP", Assert.Single((await ListAsync(Manager, term: "đặc biệt")).Items).Code);
        Assert.Equal("PO-SNAP", Assert.Single((await ListAsync(Manager, term: "đã chụp")).Items).Code);
    }

    [Fact]
    public async Task ListPage_FiltersByStatus_AndCountsOnlyThoseOrders()
    {
        await AddOrderAsync("PO-DRAFT", _supplierA, _here);
        await AddOrderAsync("PO-APPROVED-1", _supplierA, _here, status: PurchaseStatus.Approved);
        await AddOrderAsync("PO-APPROVED-2", _supplierB, _here, status: PurchaseStatus.Approved);
        await AddOrderAsync("PO-TRANSIT", _supplierA, _here, status: PurchaseStatus.InTransit);
        var keeper = new EmployeeActor(Guid.CreateVersion7(), EmployeeRole.WarehouseKeeper, _here);

        var approved = await PageAsync(Manager, status: PurchaseStatus.Approved);
        var approvedOfSupplierB = await PageAsync(Manager, supplier: _supplierB, status: PurchaseStatus.Approved);
        var draftForKeeper = await PageAsync(keeper, status: PurchaseStatus.Draft);

        Assert.Equal(2, approved.Total);
        Assert.All(approved.Items, order => Assert.Equal(PurchaseStatus.Approved, order.Status));
        Assert.Equal("PO-APPROVED-2", Assert.Single(approvedOfSupplierB.Items).Code);
        Assert.Equal(4, (await ListAsync(Manager)).Total);
        // Trạng thái không làm lộ đơn ngoài quyền xem: kho không thấy đơn nháp dù lọc đúng trạng thái đó.
        Assert.Empty(draftForKeeper.Items);
        Assert.True(draftForKeeper.AnyVisible);
    }

    [Fact]
    public async Task ListPage_AppliesTheViewRules_ToTheRowsTheTotalAndAnyVisible
()
    {
        await AddOrderAsync("PO-DRAFT-HERE", _supplierA, _here);
        await AddOrderAsync("PO-APPROVED-HERE", _supplierA, _here, status: PurchaseStatus.Approved);
        await AddOrderAsync("PO-TRANSIT-HERE", _supplierA, _here, status: PurchaseStatus.InTransit);
        await AddOrderAsync("PO-APPROVED-THERE", _supplierA, _there, status: PurchaseStatus.Approved);
        var keeperHere = new EmployeeActor(Guid.CreateVersion7(), EmployeeRole.WarehouseKeeper, _here);
        var salesHere = new EmployeeActor(Guid.CreateVersion7(), EmployeeRole.Sales, _here);

        var manager = await ListAsync(Manager);
        var keeper = await ListAsync(keeperHere);
        var sales = await ListAsync(salesHere);
        var keeperSearchingAHiddenOne = await ListAsync(keeperHere, term: "DRAFT");

        Assert.Equal(4, manager.Total);
        Assert.Equal(["PO-TRANSIT-HERE", "PO-APPROVED-HERE"], keeper.Items.Select(order => order.Code).ToArray());
        Assert.Equal(2, keeper.Total);
        Assert.Empty(sales.Items);
        Assert.False(sales.AnyVisible);
        Assert.Empty(keeperSearchingAHiddenOne.Items);
        Assert.True(keeperSearchingAHiddenOne.AnyVisible);
        Assert.Equal(0, keeperSearchingAHiddenOne.Total);
    }
}
