using Pancras.Domain.Inventory;

namespace Pancras.Application.Inventory;

internal readonly record struct BalanceSnapshot(StockBalance Balance, int OnHand, int Reserved, int InTransit)
{
    public static BalanceSnapshot Take(StockBalance balance) =>
        new(balance, balance.OnHand, balance.Reserved, balance.InTransit);

    public void Restore() => Balance.RestoreUncommitted(OnHand, Reserved, InTransit);
}
