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
    bool CustomsValueWarn,
    bool DutyWarn,
    bool VatWarn);

public static class PurchaseCosting
{
    public static PurchaseLineCost For(PurchaseOrder order, PurchaseLine line)
    {
        var fobConverted = PurchaseMoney.RoundDong(line.FobUsd * order.ExchangeRate);
        var customsAdjustment = Sum(order, line.Id, ChargeGroup.CustomsValueAdjustment);
        var expectedCustoms = fobConverted + customsAdjustment;
        var expectedDuty = expectedCustoms * line.DutyRate;
        var customsInUse = line.DeclaredCustomsValue ?? expectedCustoms;
        var dutyInUse = line.DeclaredDuty ?? expectedDuty;
        var vatBase = customsInUse
            + dutyInUse
            + line.SupplementalDuty
            + line.SpecialConsumptionTax
            + line.EnvironmentTax;
        var expectedVat = vatBase * order.VatRate;
        var vatInUse = line.DeclaredVat ?? expectedVat;
        var logistics = Sum(order, line.Id, ChargeGroup.Logistics);
        var nonDeductible = Sum(order, line.Id, ChargeGroup.NonDeductibleTax);
        var landed = customsInUse + dutyInUse + nonDeductible + logistics;
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
            line.DeclaredCustomsValue is decimal declaredCustoms && declaredCustoms != expectedCustoms,
            line.DeclaredDuty is decimal declaredDuty && declaredDuty != expectedDuty,
            line.DeclaredVat is decimal declaredVat && declaredVat != expectedVat);
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
