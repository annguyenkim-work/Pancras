using Microsoft.Extensions.Logging.Abstractions;
using Pancras.Application.Common;
using Pancras.Application.Inventory;
using Pancras.Domain.Inventory;

namespace Pancras.Application.Tests;

public class TransferSlipHandlerTests
{
    [Fact]
    public async Task Ship_RejectsTheSameLocation_WithoutInserting()
    {
        var store = new FakeInventoryStore();
        var location = StockLocation.Create("Kho A", StockLocationKind.Warehouse);
        store.Locations.Add(location);
        var itemId = Guid.CreateVersion7();
        store.Balances.Add(StockBalance.Create(itemId, location.Id, 10, 0, 0));

        var result = await Ship(store).HandleAsync(Command(location.Id, location.Id, itemId, 2), CancellationToken.None);

        Assert.Equal(StockError.InvalidTransfer, result.StockError);
        Assert.Empty(store.Transfers);
        Assert.Equal(10, store.Balances[0].OnHand);
    }

    [Fact]
    public async Task Ship_UnreservedStock_MovesOnHandOnce_AndRaisesDestinationInTransit()
    {
        var store = Store(onHand: 115);

        var result = await Ship(store.Store).HandleAsync(Command(store.Source.Id, store.Destination.Id, store.ItemId, 2), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(113, store.SourceBalance.OnHand);
        Assert.Equal(113, store.SourceBalance.Available);
        Assert.Equal(0, store.SourceBalance.Reserved);
        Assert.Equal(2, store.DestinationBalance.InTransit);
        Assert.Equal(TransferStatus.InTransit, store.Store.Transfers[0].Status);
    }

    [Fact]
    public async Task Ship_RejectsWhenAvailableIsShort_AndLeavesTheNumbers()
    {
        var store = Store(onHand: 1);

        var result = await Ship(store.Store).HandleAsync(Command(store.Source.Id, store.Destination.Id, store.ItemId, 2), CancellationToken.None);

        Assert.Equal(StockError.ShipmentRejected, result.StockError);
        Assert.Equal(1, store.SourceBalance.OnHand);
        Assert.Equal(0, store.DestinationBalance.InTransit);
        Assert.Empty(store.Store.Transfers);
    }

    [Fact]
    public async Task Ship_RejectsWhenQuantityExceedsAvailable_EvenIfOnHandIsHigher()
    {
        var store = Store(onHand: 10, reserved: 8);

        var result = await Ship(store.Store).HandleAsync(Command(store.Source.Id, store.Destination.Id, store.ItemId, 3), CancellationToken.None);

        Assert.Equal(StockError.ShipmentRejected, result.StockError);
        Assert.Equal(10, store.SourceBalance.OnHand);
        Assert.Equal(8, store.SourceBalance.Reserved);
        Assert.Equal(0, store.DestinationBalance.InTransit);
    }

    [Fact]
    public async Task Ship_RejectsADestinationThatWasDeletedOrNeverExisted()
    {
        var store = Store();
        store.Store.Locations.Remove(store.Destination);

        var result = await Ship(store.Store).HandleAsync(Command(store.Source.Id, store.Destination.Id, store.ItemId, 2), CancellationToken.None);

        Assert.Equal(StockError.InvalidTransfer, result.StockError);
        Assert.Equal(115, store.SourceBalance.OnHand);
        Assert.Empty(store.Store.Transfers);
    }

    [Fact]
    public async Task Receive_MovesInTransitIntoOnHand_AndLeavesReserved()
    {
        var store = Store(onHand: 10, reserved: 3, destinationOnHand: 4, destinationReserved: 2);
        var slipId = (await Ship(store.Store).HandleAsync(Command(store.Source.Id, store.Destination.Id, store.ItemId, 3), CancellationToken.None)).Value;
        var handler = new ConfirmTransferReceiptCommandHandler(store.Store, NullLogger<ConfirmTransferReceiptCommandHandler>.Instance);

        var result = await handler.HandleAsync(new ConfirmTransferCommand(slipId, TestActors.Admin), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(0, store.DestinationBalance.InTransit);
        Assert.Equal(7, store.DestinationBalance.OnHand);
        Assert.Equal(2, store.DestinationBalance.Reserved);
        Assert.Equal(TransferStatus.Received, store.Store.Transfers[0].Status);
    }

    [Fact]
    public async Task Cancel_ReturnsTheGoodsToTheSource_AndClearsInTransit()
    {
        var store = Store(onHand: 10);
        var slipId = (await Ship(store.Store).HandleAsync(Command(store.Source.Id, store.Destination.Id, store.ItemId, 4), CancellationToken.None)).Value;

        var result = await Cancel(store.Store).HandleAsync(new ConfirmTransferCommand(slipId, TestActors.Admin), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(10, store.SourceBalance.OnHand);
        Assert.Equal(0, store.DestinationBalance.InTransit);
        Assert.Equal(0, store.DestinationBalance.OnHand);
        Assert.Equal(TransferStatus.Cancelled, store.Store.Transfers[0].Status);
    }

    [Fact]
    public async Task Cancel_IsRejectedOnceReceived_OrCancelledTwice()
    {
        var store = Store(onHand: 10);
        var received = (await Ship(store.Store).HandleAsync(Command(store.Source.Id, store.Destination.Id, store.ItemId, 2), CancellationToken.None)).Value;
        await new ConfirmTransferReceiptCommandHandler(store.Store, NullLogger<ConfirmTransferReceiptCommandHandler>.Instance)
            .HandleAsync(new ConfirmTransferCommand(received, TestActors.Admin), CancellationToken.None);
        var cancelled = (await Ship(store.Store).HandleAsync(Command(store.Source.Id, store.Destination.Id, store.ItemId, 1), CancellationToken.None)).Value;
        await Cancel(store.Store).HandleAsync(new ConfirmTransferCommand(cancelled, TestActors.Admin), CancellationToken.None);

        var afterReceipt = await Cancel(store.Store).HandleAsync(new ConfirmTransferCommand(received, TestActors.Admin), CancellationToken.None);
        var twice = await Cancel(store.Store).HandleAsync(new ConfirmTransferCommand(cancelled, TestActors.Admin), CancellationToken.None);

        Assert.Equal(StockError.CancelRejected, afterReceipt.StockError);
        Assert.Equal(StockError.CancelRejected, twice.StockError);
        Assert.Equal(2, store.DestinationBalance.OnHand);
        Assert.Equal(8, store.SourceBalance.OnHand);
    }

    [Fact]
    public async Task Cancel_IsLimitedToTheSourceLocation()
    {
        var store = Store(onHand: 10);
        var slipId = (await Ship(store.Store).HandleAsync(Command(store.Source.Id, store.Destination.Id, store.ItemId, 2), CancellationToken.None)).Value;

        var byDestination = await Cancel(store.Store).HandleAsync(new ConfirmTransferCommand(slipId, TestActors.Keeper(store.Destination.Id)), CancellationToken.None);
        var bySource = await Cancel(store.Store).HandleAsync(new ConfirmTransferCommand(slipId, TestActors.Keeper(store.Source.Id)), CancellationToken.None);

        Assert.Equal(StockError.Forbidden, byDestination.StockError);
        Assert.True(bySource.IsSuccess);
    }

    private static CreateAndShipTransferCommandHandler Ship(FakeInventoryStore store) =>
        new(store, NullLogger<CreateAndShipTransferCommandHandler>.Instance);

    private static CancelTransferCommandHandler Cancel(FakeInventoryStore store) =>
        new(store, NullLogger<CancelTransferCommandHandler>.Instance);

    private static CreateTransferSlipCommand Command(Guid sourceId, Guid destinationId, Guid itemId, int quantity) =>
        new()
        {
            SourceLocationId = sourceId,
            DestinationLocationId = destinationId,
            Note = "Xe sáng",
            Actor = TestActors.Admin,
            Lines = [new TransferLineInput(itemId, quantity)]
        };

    private static TransferFixture Store(
        int onHand = 115,
        int reserved = 0,
        int destinationOnHand = 0,
        int destinationReserved = 0)
    {
        var store = new FakeInventoryStore();
        var source = StockLocation.Create("Kho A", StockLocationKind.Warehouse);
        var destination = StockLocation.Create("Showroom B", StockLocationKind.Showroom);
        var itemId = Guid.CreateVersion7();
        store.Locations.Add(source);
        store.Locations.Add(destination);
        var sourceBalance = StockBalance.Create(itemId, source.Id, onHand, reserved, 0);
        var destinationBalance = StockBalance.Create(itemId, destination.Id, destinationOnHand, destinationReserved, 0);
        store.Balances.Add(sourceBalance);
        store.Balances.Add(destinationBalance);
        return new TransferFixture(store, source, destination, itemId, sourceBalance, destinationBalance);
    }

    private sealed record TransferFixture(
        FakeInventoryStore Store,
        StockLocation Source,
        StockLocation Destination,
        Guid ItemId,
        StockBalance SourceBalance,
        StockBalance DestinationBalance);
}
