using Microsoft.Extensions.Logging.Abstractions;
using Pancras.Application.Purchasing;
using Pancras.Domain.Purchasing;

namespace Pancras.Application.Tests;

public class ShipmentDuplicateTests
{
    private readonly FakePurchaseStore _orders = new();

    private PurchaseOrder Add(string code, string? container, string? billOfLading)
    {
        var order = PurchaseOrder.TryCreate(code, Guid.NewGuid(), Guid.NewGuid(), container, billOfLading, null, null, 25000m, 0.1m, [(Guid.NewGuid(), 1, 10m, 0.1m, 0m, 0m, 0m)])!;
        _orders.Orders.Add(order);
        return order;
    }

    private FindShipmentDuplicatesHandler Handler() => new(_orders, NullLogger<FindShipmentDuplicatesHandler>.Instance);

    [Fact]
    public async Task SameContainerOrBillOfLading_OnAnotherOrder_IsReported_IgnoringCaseAndSpaces()
    {
        var mine = Add("PO-1", "ABCU 123", "BL-9");
        Add("PO-2", "abcu123", "OTHER");
        Add("PO-3", "ZZZ", " bl-9 ");
        Add("PO-4", "NOPE", "NOPE");

        var duplicates = await Handler().HandleAsync(mine.Id, TestActors.Accountant, CancellationToken.None);

        Assert.Equal(["PO-2", "PO-3"], duplicates.Select(item => item.Code).OrderBy(code => code));
    }

    [Fact]
    public async Task CancelledOrders_AndBlankNumbers_AreIgnored()
    {
        var mine = Add("PO-1", "C1", "");
        var cancelled = Add("PO-2", "C1", "");
        Assert.True(cancelled.TryCancel("Hủy", DateTimeOffset.UtcNow, null));
        Add("PO-3", "", "");

        var duplicates = await Handler().HandleAsync(mine.Id, TestActors.Accountant, CancellationToken.None);

        Assert.Empty(duplicates);
    }

    [Fact]
    public async Task OtherRoles_GetNothing()
    {
        var mine = Add("PO-1", "C1", "");
        Add("PO-2", "C1", "");

        Assert.Empty(await Handler().HandleAsync(mine.Id, TestActors.Sales(Guid.NewGuid()), CancellationToken.None));
    }
}
