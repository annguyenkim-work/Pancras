using Pancras.Domain.Purchasing;

namespace Pancras.Domain.Tests;

public class PurchaseCostingTests
{
    [Fact]
    public void Formula_KeepsVatDecimalAndExcludesDeductibleTax()
    {
        var itemId = Guid.CreateVersion7();
        var order = PurchaseOrder.TryCreate(
            "PO-1",
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            "CONT",
            "BL",
            "Hải Phòng",
            null,
            1m,
            0.10m,
            [(itemId, 1, 100m, 0.10m, 0m, 0m, 0m)])!;
        var line = order.Lines[0];
        Assert.True(order.TryAddCharge(20m, ChargeSplitMethod.FobValue, out var freight));
        Assert.True(order.TryAllocate(freight, new Dictionary<Guid, decimal> { [line.Id] = 100m }, false));
        Assert.True(order.TryAssignGroup(freight, ChargeGroup.CustomsValueAdjustment, false));
        Assert.True(order.TryAddCharge(8m, ChargeSplitMethod.Manual, out var thc));
        Assert.True(order.TryAllocate(thc, new Dictionary<Guid, decimal> { [line.Id] = 8m }, true));
        Assert.True(order.TryAssignGroup(thc, ChargeGroup.Logistics, false));
        Assert.True(order.TryAddCharge(13m, ChargeSplitMethod.Manual, out var vat));
        Assert.True(order.TryAllocate(vat, new Dictionary<Guid, decimal> { [line.Id] = 13m }, true));
        Assert.True(order.TryAssignGroup(vat, ChargeGroup.DeductibleTax, false));

        var cost = PurchaseCosting.For(order, line);

        Assert.Equal(100m, cost.FobConverted);
        Assert.Equal(120m, cost.ExpectedCustomsValue);
        Assert.Equal(12m, cost.ExpectedDuty);
        Assert.Equal(132m, cost.VatBase);
        Assert.Equal(13.2m, cost.ExpectedVat);
        Assert.Equal(140m, cost.LandedCost);
        Assert.Equal(8m, cost.LogisticsAllocated);
        Assert.False(order.TryAssignGroup(thc, ChargeGroup.DeductibleTax, true));
        Assert.Equal(ChargeGroup.Logistics, order.Charges.Single(charge => charge.Id == thc).Group);
    }

    [Fact]
    public void OtherNonDeductibleTaxes_AreCountedInTheLandedCost()
    {
        var order = PurchaseOrder.TryCreate(
            "PO-T",
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            null, null, null, null,
            1m,
            0.10m,
            [(Guid.CreateVersion7(), 1, 100m, 0.10m, 5m, 3m, 2m)])!;

        var cost = PurchaseCosting.For(order, order.Lines[0]);

        Assert.Equal(10m, cost.OtherTaxes);
        Assert.Equal(120m, cost.LandedCost);
    }

    [Fact]
    public void VatCountsInTheCost_OnlyWhenItIsNotDeductible()
    {
        PurchaseOrder Make(bool deductible) => PurchaseOrder.TryCreate(
            "PO-V",
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            null, null, null, null,
            1m,
            0.10m,
            [(Guid.CreateVersion7(), 1, 100m, 0.10m, 0m, 0m, 0m)],
            deductible)!;

        var deductible = Make(true);
        var notDeductible = Make(false);

        Assert.Equal(110m, PurchaseCosting.For(deductible, deductible.Lines[0]).LandedCost);
        Assert.Equal(121m, PurchaseCosting.For(notDeductible, notDeductible.Lines[0]).LandedCost);
        Assert.Equal(11m, PurchaseCosting.For(notDeductible, notDeductible.Lines[0]).VatInCost);
    }

    [Fact]
    public void AChargeWithoutAGroup_IsReported()
    {
        var order = PurchaseOrder.TryCreate(
            "PO-G",
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            null, null, null, null,
            1m,
            0m,
            [(Guid.CreateVersion7(), 1, 100m, 0m, 0m, 0m, 0m)])!;
        Assert.True(order.TryAddCharge(10m, ChargeSplitMethod.FobValue, out var chargeId, "Phí D/O"));

        Assert.True(order.HasUngroupedCharges);
        Assert.Equal("Phí D/O", order.Charges[0].Label);
        Assert.True(order.TryAssignGroup(chargeId, ChargeGroup.Logistics, false));
        Assert.False(order.HasUngroupedCharges);
    }

    [Fact]
    public void Declaration_UsesOfficialNumbersAndWarns()
    {
        var order = PurchaseOrder.TryCreate(
            "PO-1",
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            null,
            null,
            null,
            null,
            1m,
            0.10m,
            [(Guid.CreateVersion7(), 1, 100m, 0.10m, 0m, 0m, 0m)])!;
        var line = order.Lines[0];
        Assert.True(order.TryAddCharge(20m, ChargeSplitMethod.Manual, out var freight));
        Assert.True(order.TryAllocate(freight, new Dictionary<Guid, decimal> { [line.Id] = 20m }, true));
        Assert.True(order.TryAssignGroup(freight, ChargeGroup.CustomsValueAdjustment, false));
        Assert.True(order.TryDeclare(line.Id, 125m, 15m, null));

        var cost = PurchaseCosting.For(order, line);

        Assert.Equal(12m, cost.ExpectedDuty);
        Assert.Equal(125m, cost.CustomsInUse);
        Assert.Equal(15m, cost.DutyInUse);
        Assert.True(cost.CustomsValueWarn);
        Assert.True(cost.DutyWarn);
        Assert.False(cost.VatWarn);
        Assert.NotEqual(12m, cost.DutyInUse);
    }
}
