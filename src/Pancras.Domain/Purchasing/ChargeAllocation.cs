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
    /// <summary>Trần của cột numeric(18,0): số đồng lớn nhất còn lưu được.</summary>
    public const decimal MaxDong = 999_999_999_999_999_999m;

    /// <summary>Trần của cột numeric(18,2): USD lớn nhất còn lưu được.</summary>
    public const decimal MaxUsd = 9_999_999_999_999_999.99m;

    /// <summary>Trần của cột numeric(9,4): thuế suất lớn nhất còn lưu được (dạng thập phân, 1 = 100%).</summary>
    public const decimal MaxRate = 99_999.9999m;

    /// <summary>
    /// Kiểm tra từng số và tổng ước tính của đơn có nằm trong giới hạn lưu được không, trước khi lưu hay tính giá vốn.
    /// Tràn phép tính decimal cũng tính là vượt giới hạn.
    /// </summary>
    public static bool FitsLimits(
        decimal exchangeRate,
        decimal vatRate,
        IEnumerable<(decimal FobUsd, decimal DutyRate, decimal SupplementalDuty, decimal SpecialConsumptionTax, decimal EnvironmentTax)> lines,
        IEnumerable<decimal> chargeAmounts)
    {
        if (exchangeRate > MaxDong || vatRate > MaxRate)
        {
            return false;
        }

        try
        {
            var total = 0m;
            foreach (var line in lines)
            {
                if (line.FobUsd > MaxUsd
                    || line.DutyRate > MaxRate
                    || line.SupplementalDuty > MaxDong
                    || line.SpecialConsumptionTax > MaxDong
                    || line.EnvironmentTax > MaxDong)
                {
                    return false;
                }

                var goods = line.FobUsd * exchangeRate;
                total += goods + (goods * line.DutyRate) + line.SupplementalDuty + line.SpecialConsumptionTax + line.EnvironmentTax;
            }

            foreach (var amount in chargeAmounts)
            {
                if (amount > MaxDong)
                {
                    return false;
                }

                total += amount;
            }

            return total + (total * vatRate) <= MaxDong;
        }
        catch (OverflowException)
        {
            return false;
        }
    }

    public static decimal RoundDong(decimal value) =>
        decimal.Round(value, 0, MidpointRounding.AwayFromZero);

    public static decimal RoundUsd(decimal value) =>
        decimal.Round(value, 2, MidpointRounding.AwayFromZero);

    public static decimal RoundRate(decimal value) =>
        decimal.Round(value, 4, MidpointRounding.AwayFromZero);
}
