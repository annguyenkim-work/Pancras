using Pancras.Domain.Purchasing;

namespace Pancras.Application.Tests;

/// <summary>Đưa đơn mua qua luồng duyệt và nhận đủ để các test khác có sẵn đơn ở trạng thái mong muốn.</summary>
internal static class PurchaseOrderTestExtensions
{
    public static void Approve(this PurchaseOrder order)
    {
        if (order.Status == PurchaseStatus.Draft)
        {
            Assert.True(order.TrySubmit());
        }

        Assert.True(order.TryApprove());
    }

    /// <summary>Duyệt (nếu cần) rồi nhận đủ số đặt của mọi dòng, lần cuối.</summary>
    public static void ReceiveAll(this PurchaseOrder order, DateTimeOffset? at = null, string? by = null)
    {
        if (order.Status is PurchaseStatus.Draft or PurchaseStatus.PendingApproval)
        {
            order.Approve();
        }

        var actual = order.Lines.ToDictionary(line => line.Id, line => line.Quantity - line.ReceivedQuantity);
        var decision = order.TryRecordReceipt(actual, true, null, at ?? DateTimeOffset.UtcNow, by, out _);
        Assert.Equal(ReceiptDecision.Ok, decision);
    }
}
