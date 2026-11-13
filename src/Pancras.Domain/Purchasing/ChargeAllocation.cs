namespace Pancras.Domain.Purchasing;

public static class ChargeAllocation
{
    public static bool TrySplit(decimal amount, IReadOnlyList<decimal> weights, out decimal[] shares)
    {
        shares = [];
        if (weights.Count == 0 || weights.Any(weight => weight <= 0))
        {
            return false;
        }

        var total = weights.Sum();
        if (total <= 0)
        {
            return false;
        }

        var whole = (long)PurchaseMoney.RoundDong(amount);
        var result = new decimal[weights.Count];
        long used = 0;
        for (var index = 0; index < weights.Count - 1; index++)
        {
            var share = (long)decimal.Truncate(whole * weights[index] / total);
            result[index] = share;
            used += share;
        }

        var last = whole - used;
        if (last < 0)
        {
            return false;
        }

        result[^1] = last;
        shares = result;
        return true;
    }
}

public static class PurchaseMoney
{
    public static decimal RoundDong(decimal value) =>
        decimal.Round(value, 0, MidpointRounding.AwayFromZero);

    public static decimal RoundUsd(decimal value) =>
        decimal.Round(value, 2, MidpointRounding.AwayFromZero);

    public static decimal RoundRate(decimal value) =>
        decimal.Round(value, 4, MidpointRounding.AwayFromZero);
}
