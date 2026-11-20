using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Pancras.Application.Common;
using Pancras.Domain.Auditing;
using Pancras.Domain.Catalog;
using Pancras.Domain.Inventory;
using Pancras.Infrastructure.Persistence;

namespace Pancras.Infrastructure.Tests;

/// <summary>UnitOfWork tường minh: dữ liệu và nhật ký cùng lưu hoặc cùng hủy; thoát scope mà chưa commit là hủy.</summary>
public class UnitOfWorkTests(PostgresFixture fixture) : PostgresTest(fixture)
{
    private static EfUnitOfWork Uow(PancrasDbContext context) => new(context, NullLogger<EfUnitOfWork>.Instance);

    private static AuditEntry Audit(string action = "stock.test") =>
        AuditEntry.Create(DateTimeOffset.UtcNow, null, "AnNK", action, "stock_location", "x", "chi tiết", "127.0.0.1");

    [Fact]
    public async Task Commit_SavesTheDataAndTheStagedAudit_Together()
    {
        await using (var context = Database.NewContext())
        {
            await using var scope = await Uow(context).BeginAsync(CancellationToken.None);
            context.StockLocations.Add(StockLocation.Create("Kho A", StockLocationKind.Warehouse));
            new AuditLog(context, NullLogger<AuditLog>.Instance).Stage(Audit());

            Assert.Equal(SaveOutcome.Saved, await scope.CommitAsync(CancellationToken.None));
        }

        await using var read = Database.NewContext();
        Assert.Equal(1, await read.StockLocations.CountAsync());
        Assert.Equal(1, await read.AuditEntries.CountAsync());
    }

    [Fact]
    public async Task LeavingTheScopeWithoutCommit_RollsBack_AndDropsTheTrackedChanges()
    {
        await using (var context = Database.NewContext())
        {
            await using (var scope = await Uow(context).BeginAsync(CancellationToken.None))
            {
                context.StockLocations.Add(StockLocation.Create("Kho A", StockLocationKind.Warehouse));
                new AuditLog(context, NullLogger<AuditLog>.Instance).Stage(Audit());
                await context.SaveChangesAsync(); // đã ghi vào giao dịch nhưng chưa commit
            }

            Assert.Empty(context.ChangeTracker.Entries());
            Assert.Null(context.Database.CurrentTransaction);
        }

        await using var read = Database.NewContext();
        Assert.Equal(0, await read.StockLocations.CountAsync());
        Assert.Equal(0, await read.AuditEntries.CountAsync());
    }

    [Fact]
    public async Task Commit_ReturnsFailed_AndDropsTheStagedAuditToo_WhenARowBreaksAConstraint()
    {
        var location = Guid.Empty;
        await using (var context = Database.NewContext())
        {
            context.StockLocations.Add(StockLocation.Create("Kho A", StockLocationKind.Warehouse));
            await context.SaveChangesAsync();
            location = (await context.StockLocations.AsNoTracking().SingleAsync()).Id;
        }

        await using (var context = Database.NewContext())
        {
            await using var scope = await Uow(context).BeginAsync(CancellationToken.None);
            // Mặt hàng không tồn tại: vi phạm khóa ngoại.
            context.StockMovements.Add(StockMovement.Create(Guid.CreateVersion7(), location, StockMovementKind.Receipt, 1, Guid.CreateVersion7(), DateTimeOffset.UtcNow));
            new AuditLog(context, NullLogger<AuditLog>.Instance).Stage(Audit());

            Assert.Equal(SaveOutcome.Failed, await scope.CommitAsync(CancellationToken.None));
            Assert.Empty(context.ChangeTracker.Entries());
        }

        await using var read = Database.NewContext();
        Assert.Equal(0, await read.StockMovements.CountAsync());
        Assert.Equal(0, await read.AuditEntries.CountAsync());
    }

    [Fact]
    public async Task Commit_ReturnsDuplicate_ForAUniqueViolation()
    {
        await using (var seed = Database.NewContext())
        {
            seed.CatalogItems.Add(NewItem("DUP-1"));
            await seed.SaveChangesAsync();
        }

        await using var context = Database.NewContext();
        await using var scope = await Uow(context).BeginAsync(CancellationToken.None);
        context.CatalogItems.Add(NewItem("DUP-1"));

        Assert.Equal(SaveOutcome.Duplicate, await scope.CommitAsync(CancellationToken.None));
        Assert.Empty(context.ChangeTracker.Entries());
    }

    [Fact]
    public async Task Commit_ReturnsConflict_WhenSomeoneElseSavedFirst()
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
        var a = await first.StockBalances.SingleAsync();
        var b = await second.StockBalances.SingleAsync();
        Assert.True(a.TryAddOnHand(5));
        Assert.True(b.TryAddOnHand(3));

        await using (var scope = await Uow(first).BeginAsync(CancellationToken.None))
        {
            Assert.Equal(SaveOutcome.Saved, await scope.CommitAsync(CancellationToken.None));
        }

        await using (var scope = await Uow(second).BeginAsync(CancellationToken.None))
        {
            Assert.Equal(SaveOutcome.Conflict, await scope.CommitAsync(CancellationToken.None));
        }

        await using var read = Database.NewContext();
        Assert.Equal(15, (await read.StockBalances.AsNoTracking().SingleAsync()).OnHand);
    }

    [Fact]
    public async Task StandaloneRecordAsync_InsideAnOpenScope_IsSavedOnlyWhenTheScopeCommits()
    {
        await using (var context = Database.NewContext())
        {
            await using var scope = await Uow(context).BeginAsync(CancellationToken.None);
            await new AuditLog(context, NullLogger<AuditLog>.Instance).RecordAsync(Audit("stock.rolled_back"), CancellationToken.None);
        }

        await using (var context = Database.NewContext())
        {
            await using var scope = await Uow(context).BeginAsync(CancellationToken.None);
            await new AuditLog(context, NullLogger<AuditLog>.Instance).RecordAsync(Audit("stock.committed"), CancellationToken.None);
            Assert.Equal(SaveOutcome.Saved, await scope.CommitAsync(CancellationToken.None));
        }

        await using var read = Database.NewContext();
        Assert.Equal("stock.committed", (await read.AuditEntries.AsNoTracking().SingleAsync()).Action);
    }

    [Fact]
    public async Task BeginAsync_WhileAScopeIsOpen_Throws()
    {
        await using var context = Database.NewContext();
        await using var scope = await Uow(context).BeginAsync(CancellationToken.None);

        await Assert.ThrowsAsync<InvalidOperationException>(() => Uow(context).BeginAsync(CancellationToken.None));
    }

    [Fact]
    public async Task RowLockingFinders_RequireAnOpenScope()
    {
        await using var context = Database.NewContext();
        var repository = new InventoryRepository(context, NullLogger<InventoryRepository>.Instance);

        await Assert.ThrowsAsync<InvalidOperationException>(() => repository.FindTransferForUpdateAsync(Guid.CreateVersion7(), CancellationToken.None));
        await Assert.ThrowsAsync<InvalidOperationException>(() => repository.FindStocktakeForUpdateAsync(Guid.CreateVersion7(), CancellationToken.None));
        await Assert.ThrowsAsync<InvalidOperationException>(() => repository.FindAdjustmentForUpdateAsync(Guid.CreateVersion7(), CancellationToken.None));
        await Assert.ThrowsAsync<InvalidOperationException>(() => repository.FindDisposalForUpdateAsync(Guid.CreateVersion7(), CancellationToken.None));
    }

    private static CatalogItem NewItem(string code) =>
        CatalogItem.Create(code, code, ProductCategory.SanitaryEquipment, "Toto", "Việt Nam", "Cái", 1m, null, null, null, 1m, 1m, 1m, 1m, 1m);
}
