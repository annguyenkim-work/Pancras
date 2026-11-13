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
