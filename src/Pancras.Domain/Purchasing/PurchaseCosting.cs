namespace Pancras.Domain.Purchasing;

public static class LandedCostAverage
{
    public static decimal Next(int onHand, decimal currentCost, int receivedQuantity, decimal unitCost)
    {
        var unit = PurchaseMoney.RoundDong(unitCost);
        if (onHand <= 0 || receivedQuantity <= 0)
        {
            return unit;
        }

        var total = (decimal)onHand + receivedQuantity;
        return PurchaseMoney.RoundDong(((decimal)onHand * currentCost + receivedQuantity * unit) / total);
    }
}

public sealed record PurchaseLineCost(
    decimal FobConverted,
    decimal ExpectedCustomsValue,
    decimal ExpectedDuty,
    decimal CustomsInUse,
    decimal DutyInUse,
    decimal VatBase,
    decimal ExpectedVat,
    decimal VatInUse,
    decimal LogisticsAllocated,
    decimal LandedCost,
    decimal UnitCost,
    decimal OtherTaxes = 0,
    decimal NonDeductibleAllocated = 0,
    decimal VatInCost = 0);

public static class PurchaseCosting
{
    public static PurchaseLineCost For(PurchaseOrder order, PurchaseLine line)
    {
        var fobConverted = PurchaseMoney.RoundDong(line.FobUsd * order.ExchangeRate);
        var customsAdjustment = Sum(order, line.Id, ChargeGroup.CustomsValueAdjustment);
        var expectedCustoms = fobConverted + customsAdjustment;
        var expectedDuty = expectedCustoms * line.DutyRate;
        var customsInUse = expectedCustoms;
        var dutyInUse = expectedDuty;
        var vatBase = customsInUse
            + dutyInUse
            + line.SupplementalDuty
            + line.SpecialConsumptionTax
            + line.EnvironmentTax;
        var expectedVat = vatBase * order.VatRate;
        var vatInUse = expectedVat;
        var logistics = Sum(order, line.Id, ChargeGroup.Logistics);
        var nonDeductible = Sum(order, line.Id, ChargeGroup.NonDeductibleTax);
        var otherTaxes = line.SupplementalDuty + line.SpecialConsumptionTax + line.EnvironmentTax;
        var vatInCost = order.VatDeductible ? 0 : vatInUse;
        var landed = customsInUse + dutyInUse + otherTaxes + nonDeductible + logistics + vatInCost;
        var unit = line.Quantity == 0 ? 0 : landed / line.Quantity;
        return new PurchaseLineCost(
            fobConverted,
            expectedCustoms,
            expectedDuty,
            customsInUse,
            dutyInUse,
            vatBase,
            expectedVat,
            vatInUse,
            logistics,
            landed,
            unit,
            otherTaxes,
            nonDeductible,
            vatInCost);
    }

    private static decimal Sum(PurchaseOrder order, Guid lineId, ChargeGroup group)
    {
        decimal total = 0;
        foreach (var charge in order.Charges)
        {
            if (charge.Group != group)
            {
                continue;
            }

            foreach (var share in charge.Shares)
            {
                if (share.LineId == lineId)
                {
                    total += share.Amount;
                }
            }
        }

        return total;
    }
}
