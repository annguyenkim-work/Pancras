using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Pancras.Application.Auditing;
using Pancras.Application.Common;
using Pancras.Application.Inventory;
using Pancras.Application.Purchasing;
using Pancras.Domain.Auditing;
using Pancras.Domain.Employees;
using Pancras.Domain.Inventory;
using Pancras.Domain.Purchasing;
using Pancras.Infrastructure.Persistence;

namespace Pancras.Infrastructure.Tests;

/// <summary>
/// Handler thật + repository thật + <see cref="EfUnitOfWork"/> trên PostgreSQL: dữ liệu và nhật ký cùng lưu hoặc cùng hủy,
/// và handler bị từ chối giữa chừng không để lại gì (thay cho cơ chế Restore thủ công đã bỏ).
/// </summary>
public class HandlerTransactionTests(PostgresFixture fixture) : PostgresTest(fixture)
{
    private static readonly EmployeeActor Accountant = new(Guid.CreateVersion7(), EmployeeRole.Accountant, null);

    private sealed class Request : IRequestInfo
    {
        public string? IpAddress => "127.0.0.1";

        public Guid? ActorId => Accountant.Id;

        public string? ActorCode => "KT01";
    }

    private sealed class ThrowingAudit : IAuditLog
    {
        public void Stage(AuditEntry entry) => throw new InvalidOperationException("audit failed");

        public Task RecordAsync(AuditEntry entry, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<IReadOnlyList<AuditEntry>> ListPageAsync(AuditQuery query, AuditCursor? cursor, bool backward, int limit, bool newestFirst, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<AuditEntry>>([]);
    }

    private ShipTransferCommandHandler Ship(PancrasDbContext context, IAuditLog? audit = null) =>
        new(
            new InventoryRepository(context, NullLogger<InventoryRepository>.Instance),
            new EfUnitOfWork(context, NullLogger<EfUnitOfWork>.Instance),
            NullLogger<ShipTransferCommandHandler>.Instance,
            null,
            audit ?? new AuditLog(context, NullLogger<AuditLog>.Instance),
            new Request());

    /// <summary>Phiếu đã duyệt (đang giữ hàng) từ kho nguồn sang kho đích; kho nguồn có <paramref name="onHand"/> hàng.</summary>
    private async Task<(Guid Slip, Guid Source, Guid Destination, Guid Item)> SeedApprovedTransferAsync(int onHand, int quantity)
    {
        var source = await SeedLocationAsync("Kho nguồn");
        var destination = await SeedLocationAsync("Kho đích");
        var item = (await SeedItemsAsync(1))[0];
        var balance = StockBalance.Create(item, source, onHand, 0, 0);
        var slip = TransferSlip.Create(source, destination, "chuyển", [(item, quantity)])!;
        Assert.True(slip.TryApprove(new Dictionary<Guid, StockBalance> { [item] = balance }));
        await using var context = Database.NewContext();
        context.StockBalances.Add(balance);
        context.TransferSlips.Add(slip);
        await context.SaveChangesAsync();
        return (slip.Id, source, destination, item);
    }

    [Fact]
    public async Task Ship_SavesTheShipmentAndItsAuditEntry_InOneTransaction()
    {
        var (slip, source, destination, item) = await SeedApprovedTransferAsync(10, 4);

        await using (var context = Database.NewContext())
        {
            var result = await Ship(context).HandleAsync(new ShipTransferCommand(slip, Accountant), CancellationToken.None);
            Assert.True(result.IsSuccess);
        }

        await using var read = Database.NewContext();
        var sourceBalance = await read.StockBalances.AsNoTracking().SingleAsync(balance => balance.StockLocationId == source);
        var destinationBalance = await read.StockBalances.AsNoTracking().SingleAsync(balance => balance.StockLocationId == destination);
        Assert.Equal(6, sourceBalance.OnHand);
        Assert.Equal(0, sourceBalance.Reserved);
        Assert.Equal(4, destinationBalance.InTransit);
        Assert.Equal(TransferStatus.InTransit, (await read.TransferSlips.AsNoTracking().SingleAsync()).Status);
        Assert.Equal(1, await read.StockMovements.CountAsync(movement => movement.CatalogItemId == item));
        var audit = await read.AuditEntries.AsNoTracking().SingleAsync();
        Assert.Equal(AuditActions.TransferShipped, audit.Action);
        Assert.Equal(slip.ToString("D"), audit.TargetId);
    }

    [Fact]
    public async Task Ship_WhenStockIsMissing_ReportsTheShortageAndLeavesNothingBehind()
    {
        var (slip, source, _, _) = await SeedApprovedTransferAsync(10, 4);
        await using (var edit = Database.NewContext())
        {
            // Hàng bị rút bớt sau khi duyệt: tồn thực chỉ còn 2, không đủ xuất 4.
            var balance = await edit.StockBalances.SingleAsync(item => item.StockLocationId == source);
            Assert.True(balance.TryApplyCount(-8));
            await edit.SaveChangesAsync();
        }

        await using (var context = Database.NewContext())
        {
            var result = await Ship(context).HandleAsync(new ShipTransferCommand(slip, Accountant), CancellationToken.None);

            Assert.True(result.IsFailure);
            Assert.Equal(StockError.InsufficientStock, result.Error);
            Assert.Empty(context.ChangeTracker.Entries());
        }

        await using var read = Database.NewContext();
        Assert.Equal(TransferStatus.Approved, (await read.TransferSlips.AsNoTracking().SingleAsync()).Status);
        Assert.Equal(2, (await read.StockBalances.AsNoTracking().SingleAsync(balance => balance.StockLocationId == source)).OnHand);
        Assert.Equal(0, await read.StockMovements.CountAsync());
        Assert.Equal(0, await read.AuditEntries.CountAsync());
    }

    [Fact]
    public async Task Ship_RollsEverythingBack_WhenTheAuditEntryCannotBeStaged()
    {
        var (slip, source, _, _) = await SeedApprovedTransferAsync(10, 4);

        await using (var context = Database.NewContext())
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                Ship(context, new ThrowingAudit()).HandleAsync(new ShipTransferCommand(slip, Accountant), CancellationToken.None));
        }

        await using var read = Database.NewContext();
        Assert.Equal(TransferStatus.Approved, (await read.TransferSlips.AsNoTracking().SingleAsync()).Status);
        Assert.Equal(10, (await read.StockBalances.AsNoTracking().SingleAsync(balance => balance.StockLocationId == source)).OnHand);
        Assert.Equal(0, await read.StockMovements.CountAsync());
    }

    [Fact]
    public async Task Ship_ByAnotherRequest_AfterTheFirstShipped_IsRejectedWithoutChangingStockTwice()
    {
        var (slip, source, _, _) = await SeedApprovedTransferAsync(10, 4);
        await using (var first = Database.NewContext())
        {
            Assert.True((await Ship(first).HandleAsync(new ShipTransferCommand(slip, Accountant), CancellationToken.None)).IsSuccess);
        }

        await using (var second = Database.NewContext())
        {
            var again = await Ship(second).HandleAsync(new ShipTransferCommand(slip, Accountant), CancellationToken.None);
            Assert.Equal(StockError.TransferStateChanged, again.Error);
        }

        await using var read = Database.NewContext();
        Assert.Equal(6, (await read.StockBalances.AsNoTracking().SingleAsync(balance => balance.StockLocationId == source)).OnHand);
        Assert.Equal(1, await read.AuditEntries.CountAsync());
    }

    [Fact]
    public async Task ReceiptPreCheckAndReadOnlyFinder_RunWithoutAScope_AndLeaveNoTransactionOpen()
    {
        var (slip, _, destination, item) = await SeedApprovedTransferAsync(10, 4);
        await using (var ship = Database.NewContext())
        {
            Assert.True((await Ship(ship).HandleAsync(new ShipTransferCommand(slip, Accountant), CancellationToken.None)).IsSuccess);
        }

        await using var context = Database.NewContext();
        var repository = new InventoryRepository(context, NullLogger<InventoryRepository>.Instance);
        var handler = new ConfirmTransferReceiptCommandHandler(
            repository,
            new EfUnitOfWork(context, NullLogger<EfUnitOfWork>.Instance),
            NullLogger<ConfirmTransferReceiptCommandHandler>.Instance);

        var found = await repository.FindTransferReadOnlyAsync(slip, CancellationToken.None);
        var lineId = Assert.Single(found!.Lines).Id;
        var check = await handler.CheckAsync(
            new ConfirmTransferCommand(slip, Accountant, [new ReceivedLineInput(lineId, 4)]),
            0,
            CancellationToken.None);

        Assert.True(check.IsSuccess);
        Assert.Null(context.Database.CurrentTransaction);
        Assert.Equal(destination, found.DestinationLocationId);
        Assert.NotEqual(Guid.Empty, item);
    }

    [Fact]
    public async Task ReceivePurchase_SavesStockCostAndAudit_AndARejectedReceiptLeavesNothing()
    {
        var supplier = await SeedSupplierAsync();
        var location = await SeedLocationAsync();
        var item = (await SeedItemsAsync(1))[0];
        var order = PurchaseOrder.TryCreate(
            "PO-RX-" + Guid.NewGuid().ToString("N")[..6], supplier, location, "CONT", null, null, null, 25000m, 0m,
            [(item, 5, 10m, 0m, 0m, 0m, 0m)])!;
        Assert.True(order.TrySubmit());
        Assert.True(order.TryApprove());
        await using (var seed = Database.NewContext())
        {
            await new PurchaseOrderRepository(seed).AddAsync(order, CancellationToken.None);
            Assert.Equal(SaveOutcome.Saved, await CommitAsync(seed));
        }

        ReceivePurchaseOrderCommandHandler Handler(PancrasDbContext context) =>
            new(
                new PurchaseOrderRepository(context),
                new InventoryRepository(context, NullLogger<InventoryRepository>.Instance),
                new CatalogItemRepository(context),
                new EfUnitOfWork(context, NullLogger<EfUnitOfWork>.Instance),
                NullLogger<ReceivePurchaseOrderCommandHandler>.Instance,
                null,
                new AuditLog(context, NullLogger<AuditLog>.Instance),
                new Request());

        // Nhận quá số đặt khi chưa phải lần cuối: bị từ chối, không đổi gì.
        await using (var rejected = Database.NewContext())
        {
            var line = order.Lines[0].Id;
            var result = await Handler(rejected).HandleAsync(
                new ReceivePurchaseOrderCommand(order.Id, Accountant, new Dictionary<Guid, int> { [line] = 99 }, Final: false),
                CancellationToken.None);
            Assert.True(result.IsFailure);
        }

        await using (var check = Database.NewContext())
        {
            Assert.Equal(0, await check.StockBalances.CountAsync());
            Assert.Equal(0, await check.StockMovements.CountAsync());
            Assert.Equal(0, await check.AuditEntries.CountAsync());
            Assert.Equal(PurchaseStatus.Approved, (await check.PurchaseOrders.AsNoTracking().SingleAsync()).Status);
        }

        await using (var accepted = Database.NewContext())
        {
            var result = await Handler(accepted).HandleAsync(new ReceivePurchaseOrderCommand(order.Id, Accountant), CancellationToken.None);
            Assert.True(result.IsSuccess);
        }

        await using var read = Database.NewContext();
        Assert.Equal(5, (await read.StockBalances.AsNoTracking().SingleAsync()).OnHand);
        Assert.Equal(1, await read.StockMovements.CountAsync());
        Assert.Equal(PurchaseStatus.Received, (await read.PurchaseOrders.AsNoTracking().SingleAsync()).Status);
        Assert.Equal(AuditActions.PurchaseReceived, (await read.AuditEntries.AsNoTracking().SingleAsync()).Action);
        Assert.True((await read.CatalogItems.AsNoTracking().SingleAsync(candidate => candidate.Id == item)).CostPrice > 0);
    }
}
