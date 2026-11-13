using Microsoft.Extensions.Logging.Abstractions;
using Pancras.Application.Common;
using Pancras.Application.Inventory;
using Pancras.Domain.Inventory;

namespace Pancras.Application.Tests;

public class TransferSlipHandlerTests
{
    [Fact]
    public async Task Create_RejectsTheSameLocation_WithoutInserting()
    {
        var store = new FakeInventoryStore();
        var location = StockLocation.Create("Kho A", StockLocationKind.Warehouse);
        var itemId = Guid.CreateVersion7();
        store.Balances.Add(StockBalance.Create(itemId, location.Id, 10, 0, 0));
        var handler = new CreateTransferSlipCommandHandler(store, NullLogger<CreateTransferSlipCommandHandler>.Instance);

        var result = await handler.HandleAsync(Command(location.Id, location.Id, itemId, 2), CancellationToken.None);

        Assert.Equal(StockError.InvalidTransfer, result.StockError);
        Assert.Empty(store.Transfers);
        Assert.Equal(10, store.Balances[0].OnHand);
    }

    [Fact]
    public async Task Create_Draft_DoesNotChangeStock()
    {
        var store = Store();
        var handler = new CreateTransferSlipCommandHandler(store.Store, NullLogger<CreateTransferSlipCommandHandler>.Instance);

        var result = await handler.HandleAsync(Command(store.Source.Id, store.Destination.Id, store.ItemId, 2), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(TransferStatus.Draft, Assert.Single(store.Store.Transfers).Status);
        Assert.Equal(115, store.SourceBalance.OnHand);
        Assert.Equal(0, store.SourceBalance.Reserved);
        Assert.Equal(0, store.DestinationBalance.InTransit);
    }

    [Fact]
    public async Task Ship_UnreservedStock_MovesOnHandOnce_AndRaisesDestinationInTransit()
    {
        var store = Store(onHand: 115);
        var slipId = await CreateAsync(store, 2);
        var handler = new ConfirmTransferShipmentCommandHandler(store.Store, NullLogger<ConfirmTransferShipmentCommandHandler>.Instance);

        var result = await handler.HandleAsync(new ConfirmTransferCommand(slipId), CancellationToken.None);

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
        var slipId = await CreateAsync(store, 2);
        var handler = new ConfirmTransferShipmentCommandHandler(store.Store, NullLogger<ConfirmTransferShipmentCommandHandler>.Instance);

        var result = await handler.HandleAsync(new ConfirmTransferCommand(slipId), CancellationToken.None);

        Assert.Equal(StockError.ShipmentRejected, result.StockError);
        Assert.Equal(1, store.SourceBalance.OnHand);
        Assert.Equal(0, store.SourceBalance.Reserved);
        Assert.Equal(0, store.DestinationBalance.InTransit);
        Assert.Equal(TransferStatus.Draft, store.Store.Transfers[0].Status);
    }

    [Fact]
    public async Task Ship_PartialHold_MovesOnlyAvailable_AndLeavesReserved()
    {
        var store = Store(onHand: 10, reserved: 1);
        var slipId = await CreateAsync(store, 2);
        var handler = new ConfirmTransferShipmentCommandHandler(store.Store, NullLogger<ConfirmTransferShipmentCommandHandler>.Instance);

        var first = await handler.HandleAsync(new ConfirmTransferCommand(slipId), CancellationToken.None);
        var second = await handler.HandleAsync(new ConfirmTransferCommand(slipId), CancellationToken.None);

        Assert.True(first.IsSuccess);
        Assert.Equal(StockError.ShipmentRejected, second.StockError);
        Assert.Equal(8, store.SourceBalance.OnHand);
        Assert.Equal(1, store.SourceBalance.Reserved);
        Assert.Equal(7, store.SourceBalance.Available);
        Assert.Equal(2, store.DestinationBalance.InTransit);
    }

    [Fact]
    public async Task Ship_RejectsWhenQuantityExceedsAvailable_EvenIfOnHandIsHigher()
    {
        var store = Store(onHand: 10, reserved: 8);
        var slipId = await CreateAsync(store, 3);
        var handler = new ConfirmTransferShipmentCommandHandler(store.Store, NullLogger<ConfirmTransferShipmentCommandHandler>.Instance);

        var result = await handler.HandleAsync(new ConfirmTransferCommand(slipId), CancellationToken.None);

        Assert.Equal(StockError.ShipmentRejected, result.StockError);
        Assert.Equal(10, store.SourceBalance.OnHand);
        Assert.Equal(8, store.SourceBalance.Reserved);
        Assert.Equal(2, store.SourceBalance.Available);
        Assert.Equal(0, store.DestinationBalance.InTransit);
        Assert.Equal(TransferStatus.Draft, store.Store.Transfers[0].Status);
    }

    [Fact]
    public async Task Receive_MovesInTransitIntoOnHand_AndLeavesReserved()
    {
        var store = Store(onHand: 10, reserved: 3, destinationOnHand: 4, destinationReserved: 2);
        var slipId = await CreateAsync(store, 3);
        var shipment = new ConfirmTransferShipmentCommandHandler(store.Store, NullLogger<ConfirmTransferShipmentCommandHandler>.Instance);
        await shipment.HandleAsync(new ConfirmTransferCommand(slipId), CancellationToken.None);
        var handler = new ConfirmTransferReceiptCommandHandler(store.Store, NullLogger<ConfirmTransferReceiptCommandHandler>.Instance);

        var result = await handler.HandleAsync(new ConfirmTransferCommand(slipId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(0, store.DestinationBalance.InTransit);
        Assert.Equal(7, store.DestinationBalance.OnHand);
        Assert.Equal(5, store.DestinationBalance.Available);
        Assert.Equal(2, store.DestinationBalance.Reserved);
        Assert.Equal(TransferStatus.Received, store.Store.Transfers[0].Status);
    }

    private static async Task<Guid> CreateAsync(TransferFixture store, int quantity)
    {
        var handler = new CreateTransferSlipCommandHandler(store.Store, NullLogger<CreateTransferSlipCommandHandler>.Instance);
        var result = await handler.HandleAsync(Command(store.Source.Id, store.Destination.Id, store.ItemId, quantity), CancellationToken.None);
        return result.Value!;
    }

    private static CreateTransferSlipCommand Command(Guid sourceId, Guid destinationId, Guid itemId, int quantity) =>
        new()
        {
            SourceLocationId = sourceId,
            DestinationLocationId = destinationId,
            Note = "Xe sáng",
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
