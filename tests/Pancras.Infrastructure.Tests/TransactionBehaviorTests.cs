using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Pancras.Application.Common;
using Pancras.Domain.Inventory;
using Pancras.Domain.Purchasing;
using Pancras.Infrastructure.Persistence;

namespace Pancras.Infrastructure.Tests;

/// <summary>
/// Hành vi giao dịch, khóa dòng và concurrency của tầng lưu trữ trên PostgreSQL thật. InMemory không kiểm được các điều này.
/// Các test mở scope IUnitOfWork giống một handler: repository chỉ đọc/staging, scope commit.
/// </summary>
public class TransactionBehaviorTests(PostgresFixture fixture) : PostgresTest(fixture)
{
    private static InventoryRepository Inventory(PancrasDbContext context) =>
        new(context, NullLogger<InventoryRepository>.Instance);

    private static PurchaseOrderRepository Orders(PancrasDbContext context) => new(context);

    private static EfUnitOfWork Uow(PancrasDbContext context) => new(context, NullLogger<EfUnitOfWork>.Instance);

    [Fact]
    public async Task Commit_RollsBackEveryRow_WhenOneRowBreaksAConstraint()
    {
        var location = await SeedLocationAsync();
        var item = (await SeedItemsAsync(1))[0];
        await using (var context = Database.NewContext())
        {
            var repository = Inventory(context);
            await repository.AddBalanceAsync(StockBalance.Create(item, location, 5, 0, 0), CancellationToken.None);
            // Mặt hàng không tồn tại: vi phạm khóa ngoại, cả lần lưu phải bị hủy.
            await repository.AddMovementAsync(
                StockMovement.Create(Guid.CreateVersion7(), location, StockMovementKind.Receipt, 5, Guid.CreateVersion7(), DateTimeOffset.UtcNow),
                CancellationToken.None);

            Assert.Equal(SaveOutcome.Failed, await CommitAsync(context));
        }

        await using var read = Database.NewContext();
        Assert.Equal(0, await read.StockBalances.CountAsync());
        Assert.Equal(0, await read.StockMovements.CountAsync());
    }

    [Fact]
    public async Task Commit_RejectsTheSecondWriterOfAStockBalance_AndKeepsTheFirstValue()
    {
        var location = await SeedLocationAsync();
        var item = (await SeedItemsAsync(1))[0];
        await using (var seed = Database.NewContext())
        {
            seed.StockBalances.Add(StockBalance.Create(item, location, 10, 0, 0));
            await seed.SaveChangesAsync();
        }

        await using var first = Database.NewContext();
        await using var second = Database.NewContext();
        var a = await Inventory(first).FindBalanceForUpdateAsync(item, location, CancellationToken.None);
        var b = await Inventory(second).FindBalanceForUpdateAsync(item, location, CancellationToken.None);
        Assert.True(a!.TryAddOnHand(5));
        Assert.True(b!.TryAddOnHand(3));

        Assert.Equal(SaveOutcome.Saved, await CommitAsync(first));
        Assert.Equal(SaveOutcome.Conflict, await CommitAsync(second));

        await using var read = Database.NewContext();
        Assert.Equal(15, (await read.StockBalances.AsNoTracking().SingleAsync()).OnHand);
    }

    [Fact]
    public async Task FindTransferForUpdate_MakesASecondLockerWait_AndThenShowsTheCommittedChange()
    {
        var source = await SeedLocationAsync("Kho nguồn");
        var destination = await SeedLocationAsync("Kho đích");
        var item = (await SeedItemsAsync(1))[0];
        var slip = TransferSlip.Create(source, destination, "cũ", [(item, 1)])!;
        await using (var seed = Database.NewContext())
        {
            seed.TransferSlips.Add(slip);
            await seed.SaveChangesAsync();
        }

        await using var first = Database.NewContext();
        await using var second = Database.NewContext();
        await using var firstScope = await Uow(first).BeginAsync(CancellationToken.None);
        await using var secondScope = await Uow(second).BeginAsync(CancellationToken.None);
        var held = await Inventory(first).FindTransferForUpdateAsync(slip.Id, CancellationToken.None);
        Assert.NotNull(held);

        var waiting = Inventory(second).FindTransferForUpdateAsync(slip.Id, CancellationToken.None);
        await Task.Delay(750);
        Assert.False(waiting.IsCompleted, "Người đến sau phải chờ khi dòng phiếu đang bị khóa.");

        Assert.True(held.TryUpdateRequest(source, destination, "mới", [(item, 2)]));
        Assert.Equal(SaveOutcome.Saved, await firstScope.CommitAsync(CancellationToken.None));

        var seen = await waiting.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal("mới", seen!.Note);
        Assert.Equal(SaveOutcome.Saved, await secondScope.CommitAsync(CancellationToken.None));
    }

    [Fact]
    public async Task PurchaseCommit_RejectsTheSecondWriterOfAnOrder()
    {
        var supplier = await SeedSupplierAsync();
        var location = await SeedLocationAsync();
        var item = (await SeedItemsAsync(1))[0];
        var order = PurchaseOrder.TryCreate(
            "PO-TX-" + Guid.NewGuid().ToString("N")[..6],
            supplier,
            location,
            "CONT",
            null,
            null,
            null,
            25000m,
            0.10m,
            [(item, 1, 10m, 0.10m, 0m, 0m, 0m)])!;
        await using (var seed = Database.NewContext())
        {
            await Orders(seed).AddAsync(order, CancellationToken.None);
            Assert.Equal(SaveOutcome.Saved, await CommitAsync(seed));
        }

        await using var first = Database.NewContext();
        await using var second = Database.NewContext();
        var a = await Orders(first).FindForUpdateAsync(order.Id, CancellationToken.None);
        var b = await Orders(second).FindForUpdateAsync(order.Id, CancellationToken.None);
        a!.AdvanceVersion();
        b!.AdvanceVersion();

        Assert.Equal(SaveOutcome.Saved, await CommitAsync(first));
        Assert.Equal(SaveOutcome.Conflict, await CommitAsync(second));
    }

    [Fact]
    public async Task VersionedEntity_RejectsTheSecondEditor_WithAConcurrencyException()
    {
        var location = StockLocation.Create("Kho A", StockLocationKind.Warehouse);
        await using (var seed = Database.NewContext(new AuditStampInterceptor()))
        {
            seed.StockLocations.Add(location);
            await seed.SaveChangesAsync();
        }

        await using var first = Database.NewContext(new AuditStampInterceptor());
        await using var second = Database.NewContext(new AuditStampInterceptor());
        var a = await first.StockLocations.SingleAsync(item => item.Id == location.Id);
        var b = await second.StockLocations.SingleAsync(item => item.Id == location.Id);
        a.Update("Kho A1", StockLocationKind.Warehouse);
        b.Update("Kho A2", StockLocationKind.Warehouse);

        await first.SaveChangesAsync();
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => second.SaveChangesAsync());

        await using var read = Database.NewContext();
        Assert.Equal("Kho A1", (await read.StockLocations.AsNoTracking().SingleAsync(item => item.Id == location.Id)).Name);
    }
}
