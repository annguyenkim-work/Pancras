using Pancras.Domain.Inventory;

namespace Pancras.Domain.Tests;

public class StockBalanceTests
{
    [Fact]
    public void Available_IsOnHandMinusReserved_AndIgnoresInTransit()
    {
        var balance = StockBalance.Create(Guid.CreateVersion7(), Guid.CreateVersion7(), 4, 1, 9);

        Assert.Equal(3, balance.Available);
        Assert.Equal(4, balance.OnHand);
        Assert.Equal(1, balance.Reserved);
        Assert.Equal(9, balance.InTransit);
        Assert.NotEqual(balance.OnHand - balance.Reserved + balance.InTransit, balance.Available);
    }

    [Fact]
    public void Create_RejectsOnHandBelowReserved_WithoutReturningABalance()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            StockBalance.Create(Guid.CreateVersion7(), Guid.CreateVersion7(), 1, 2, 0));
    }

    [Fact]
    public void Available_StaysNonNegative_WhenReservedDoesNotExceedOnHand()
    {
        var balance = StockBalance.Create(Guid.CreateVersion7(), Guid.CreateVersion7(), 5, 5, 3);

        Assert.Equal(0, balance.Available);
        Assert.True(balance.Available >= 0);
    }
}

public class StockReservationTests
{
    [Fact]
    public void Reserve_UsesAvailableStock_AndShippingConsumesTheReservation()
    {
        var balance = StockBalance.Create(Guid.NewGuid(), Guid.NewGuid(), 10, 2, 0);

        Assert.False(balance.TryReserve(9));
        Assert.True(balance.TryReserve(8));
        Assert.Equal(10, balance.Reserved);
        Assert.Equal(0, balance.Available);
        Assert.True(balance.TryShipReserved(8));

        Assert.Equal(2, balance.OnHand);
        Assert.Equal(2, balance.Reserved);
    }

    [Fact]
    public void Release_GivesTheReservedStockBack_ButNeverMoreThanReserved()
    {
        var balance = StockBalance.Create(Guid.NewGuid(), Guid.NewGuid(), 10, 4, 0);

        Assert.False(balance.TryRelease(5));
        Assert.True(balance.TryRelease(4));
        Assert.Equal(10, balance.Available);
        Assert.False(balance.TryShipReserved(1));
    }

    [Fact]
    public void ReturnToSource_RestoresStock_OnlyWhileInTransit()
    {
        var item = Guid.NewGuid();
        var source = StockBalance.Create(item, Guid.NewGuid(), 6, 0, 0);
        var destination = StockBalance.Create(item, Guid.NewGuid(), 0, 0, 4);
        var slip = TransferSlip.Create(source.StockLocationId, destination.StockLocationId, null, [(item, 4)])!;
        var sources = new Dictionary<Guid, StockBalance> { [item] = source };
        var destinations = new Dictionary<Guid, StockBalance> { [item] = destination };

        Assert.False(slip.TryReturnToSource(sources, destinations, "Giao nhầm"));
        Assert.True(slip.TryApprove(sources));
        Assert.True(slip.TryConfirmShipment(sources, new Dictionary<Guid, StockBalance> { [item] = destination }));
        Assert.False(slip.TryReturnToSource(sources, destinations, " "));
        Assert.True(slip.TryReturnToSource(sources, destinations, "Giao nhầm"));

        Assert.Equal(TransferStatus.Returned, slip.Status);
        Assert.Equal(6, source.OnHand);
        Assert.Equal(0, source.Reserved);
        Assert.Equal(4, destination.InTransit);
    }
}
