using Pancras.Domain.Inventory;

namespace Pancras.Domain.Tests;

/// <summary>Luật in phiếu chuyển kho (docs/04-domain/modules/inventory.md, mục "Phiếu chuyển kho in được").</summary>
public class TransferPrintRulesTests
{
    [Theory]
    [InlineData(TransferStatus.Approved)]
    [InlineData(TransferStatus.InTransit)]
    [InlineData(TransferStatus.Received)]
    [InlineData(TransferStatus.ReceivedWithDiscrepancy)]
    [InlineData(TransferStatus.Returned)]
    public void CanPrint_IsTrue_FromApprovedOnwards(TransferStatus status) =>
        Assert.True(TransferSlip.CanPrint(status));

    [Theory]
    [InlineData(TransferStatus.Draft)]
    [InlineData(TransferStatus.Requested)]
    [InlineData(TransferStatus.Rejected)]
    [InlineData(TransferStatus.Cancelled)]
    public void CanPrint_IsFalse_BeforeApprovalAndForRejectedOrCancelled(TransferStatus status) =>
        Assert.False(TransferSlip.CanPrint(status));

    [Theory]
    [InlineData(1, "CK-000001")]
    [InlineData(123, "CK-000123")]
    [InlineData(999999, "CK-999999")]
    [InlineData(1234567, "CK-1234567")]
    public void ForTransfer_PadsTheSequenceNumberToSixDigits(int number, string expected) =>
        Assert.Equal(expected, StockDocumentCode.ForTransfer(number));

    [Fact]
    public void NewSlip_HasNoSequenceNumberUntilItIsSaved()
    {
        var slip = TransferSlip.Create(Guid.CreateVersion7(), Guid.CreateVersion7(), null, [(Guid.CreateVersion7(), 1)])!;

        Assert.Equal(0, slip.Number);
    }
}
