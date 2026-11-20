using Pancras.Domain.Purchasing;

namespace Pancras.Domain.Tests;

/// <summary>Không bao giờ nhận quá số đặt, kể cả lần cuối (docs/04-domain/modules/purchasing.md mục 4, chốt 2026-11-21).</summary>
public class PurchaseReceiptLimitTests
{
    private static PurchaseOrder ApprovedOrder(int quantity = 10)
    {
        var order = PurchaseOrder.TryCreate("PO-LIM", Guid.CreateVersion7(), Guid.CreateVersion7(), "CONT", null, null, null, 1m, 0m,
            [(Guid.CreateVersion7(), quantity, 100m, 0m, 0m, 0m, 0m)])!;
        Assert.True(order.TrySubmit());
        Assert.True(order.TryApprove());
        return order;
    }

    private static ReceiptDecision Receive(PurchaseOrder order, int quantity, bool final, string? note = null) =>
        order.TryRecordReceipt(new Dictionary<Guid, int> { [order.Lines[0].Id] = quantity }, final, note, DateTimeOffset.UtcNow, "kho", out _);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ReceivingMoreThanOrdered_IsRejected_PartialOrFinal(bool final)
    {
        var order = ApprovedOrder();

        Assert.Equal(ReceiptDecision.ExceedsOrdered, Receive(order, 11, final, "thừa hàng"));
        Assert.Equal(PurchaseStatus.Approved, order.Status);
        Assert.Equal(0, order.Lines[0].ReceivedQuantity);
    }

    [Fact]
    public void TheLimitIsWhatIsStillToReceive_AfterAPartialReceipt()
    {
        var order = ApprovedOrder();
        Assert.Equal(ReceiptDecision.Ok, Receive(order, 6, final: false));

        Assert.Equal(ReceiptDecision.ExceedsOrdered, Receive(order, 5, final: true, "thừa"));
        Assert.Equal(6, order.Lines[0].ReceivedQuantity);
        Assert.Equal(ReceiptDecision.Ok, Receive(order, 4, final: true));
        Assert.Equal(PurchaseStatus.Received, order.Status);
    }

    [Fact]
    public void ClosingShort_StillNeedsANote_AndExactlyTheRemainderNeedsNone()
    {
        var order = ApprovedOrder();

        Assert.Equal(ReceiptDecision.NoteRequired, Receive(order, 7, final: true));
        Assert.Equal(ReceiptDecision.Ok, Receive(order, 7, final: true, "thiếu 3 do vỡ"));
        Assert.Equal(PurchaseStatus.Received, order.Status);
    }
}
