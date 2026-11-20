using Microsoft.EntityFrameworkCore;
using Pancras.Domain.Inventory;
using Pancras.Infrastructure.Persistence;

namespace Pancras.Infrastructure.Tests;

/// <summary>Thay danh sách dòng của phiếu đã lưu phải INSERT dòng mới và xóa dòng cũ, không phải UPDATE dòng chưa tồn tại.</summary>
public class TransferPersistenceTests(PostgresFixture fixture) : PostgresTest(fixture)
{
    [Fact]
    public async Task UpdatingARequest_ReplacesItsLines()
    {
        var source = await SeedLocationAsync("Kho nguồn");
        var destination = await SeedLocationAsync("Kho đích");
        var items = await SeedItemsAsync(2);
        var first = items[0];
        var second = items[1];
        var slip = TransferSlip.Create(source, destination, "cũ", [(first, 2)])!;
        await using (var context = new PancrasDbContext(Options))
        {
            context.TransferSlips.Add(slip);
            await context.SaveChangesAsync();
        }

        await using (var context = new PancrasDbContext(Options))
        {
            var tracked = await context.TransferSlips.Include(item => item.Lines).SingleAsync(item => item.Id == slip.Id);
            Assert.True(tracked.TryUpdateRequest(source, destination, "mới", [(first, 5), (second, 1)]));
            await context.SaveChangesAsync();
        }

        await using var read = new PancrasDbContext(Options);
        var saved = await read.TransferSlips.AsNoTracking().Include(item => item.Lines).SingleAsync(item => item.Id == slip.Id);
        Assert.Equal("mới", saved.Note);
        Assert.Equal([5, 1], saved.Lines.OrderBy(line => line.Quantity == 5 ? 0 : 1).Select(line => line.Quantity).ToArray());
        Assert.Equal(2, await read.TransferLines.CountAsync());
    }
}

public class TransferTrailPersistenceTests(PostgresFixture fixture) : PostgresTest(fixture)
{
    [Fact]
    public async Task Slip_KeepsItsCreatorAndReservationFlag_AndItsHistoryEvents()
    {
        var creator = Guid.CreateVersion7();
        var slip = TransferSlip.Create(await SeedLocationAsync("Kho nguồn"), await SeedLocationAsync("Kho đích"), "ghi chú", [((await SeedItemsAsync(1))[0], 3)], creator)!;
        await using (var context = new PancrasDbContext(Options))
        {
            context.TransferSlips.Add(slip);
            context.TransferEvents.Add(TransferEvent.Create(slip.Id, TransferEventKind.Requested, "AnNK", DateTimeOffset.UtcNow, "ghi chú"));
            context.TransferEvents.Add(TransferEvent.Create(slip.Id, TransferEventKind.Cancelled, "AnNK", DateTimeOffset.UtcNow.AddMinutes(1), "Không cần"));
            await context.SaveChangesAsync();
        }

        await using var read = new PancrasDbContext(Options);
        var saved = await read.TransferSlips.AsNoTracking().SingleAsync(item => item.Id == slip.Id);
        var events = await read.TransferEvents.AsNoTracking().Where(item => item.TransferSlipId == slip.Id).OrderBy(item => item.OccurredAt).ToListAsync();

        Assert.Equal(creator, saved.RequestedById);
        Assert.False(saved.StockReserved);
        Assert.Equal([TransferEventKind.Requested, TransferEventKind.Cancelled], events.Select(item => item.Kind).ToArray());
        Assert.Equal("Không cần", events[1].Note);
    }

    [Fact]
    public async Task RecordedAdjustment_PointsBackToTheTransferSlip()
    {
        var slipId = Guid.CreateVersion7();
        var record = QuantityAdjustment.TryRecordTransferVariance(slipId, await SeedLocationAsync(), (await SeedItemsAsync(1))[0], -2, "Thiếu 2", "CK-ABCD1234")!;
        await using (var context = new PancrasDbContext(Options))
        {
            context.QuantityAdjustments.Add(record);
            await context.SaveChangesAsync();
        }

        await using var read = new PancrasDbContext(Options);
        var saved = await read.QuantityAdjustments.AsNoTracking().SingleAsync(item => item.Id == record.Id);

        Assert.Equal(ApprovalStatus.Recorded, saved.Status);
        Assert.Equal(slipId, saved.TransferSlipId);
        Assert.Equal(-2, saved.Delta);
    }
}
