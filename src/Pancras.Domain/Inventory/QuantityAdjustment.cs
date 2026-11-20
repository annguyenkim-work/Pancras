namespace Pancras.Domain.Inventory;

public sealed class QuantityAdjustment : Pancras.Domain.Auditing.AuditStamped
{
    private QuantityAdjustment()
    {
        Reason = string.Empty;
        DocumentRef = string.Empty;
        ImageRef = string.Empty;
    }

    public Guid Id { get; private set; }

    public Guid StockLocationId { get; private set; }

    public Guid CatalogItemId { get; private set; }

    public int Delta { get; private set; }

    public string Reason { get; private set; }

    public string DocumentRef { get; private set; }

    public string ImageRef { get; private set; }

    public ApprovalStatus Status { get; private set; }

    /// <summary>Phiếu chuyển kho sinh ra phiếu này (phiếu ghi nhận chênh lệch khi nhận hàng); null với phiếu lập tay.</summary>
    public Guid? TransferSlipId { get; private set; }

    /// <summary>
    /// Phiếu ghi nhận phần thừa/thiếu khi nhận hàng chuyển kho. Tồn kho đích đã bằng số thực nhận nên không cộng trừ lại.
    /// </summary>
    public static QuantityAdjustment? TryRecordTransferVariance(
        Guid transferSlipId,
        Guid stockLocationId,
        Guid catalogItemId,
        int delta,
        string? reason,
        string? documentRef)
    {
        if (transferSlipId == Guid.Empty
            || stockLocationId == Guid.Empty
            || catalogItemId == Guid.Empty
            || delta == 0
            || !StocktakeSheet.TryEvidence(true, reason, documentRef, null, out var storedReason, out var storedDocument, out var storedImage))
        {
            return null;
        }

        return new QuantityAdjustment
        {
            Id = Guid.CreateVersion7(),
            StockLocationId = stockLocationId,
            CatalogItemId = catalogItemId,
            Delta = delta,
            Reason = storedReason,
            DocumentRef = storedDocument,
            ImageRef = storedImage,
            Status = ApprovalStatus.Recorded,
            TransferSlipId = transferSlipId
        };
    }

    public static StockDecision TryCreate(
        string? role,
        Guid stockLocationId,
        Guid catalogItemId,
        int delta,
        string? reason,
        string? documentRef,
        string? imageRef,
        out QuantityAdjustment? adjustment)
    {
        adjustment = null;
        if (!InventoryRoles.CanDraftAdjustment(role))
        {
            return StockDecision.WrongRole;
        }

        if (stockLocationId == Guid.Empty || catalogItemId == Guid.Empty)
        {
            return StockDecision.Rejected;
        }

        if (delta == 0)
        {
            return StockDecision.InvalidQuantity;
        }

        if (!StocktakeSheet.TryEvidence(true, reason, documentRef, imageRef, out var storedReason, out var storedDocument, out var storedImage))
        {
            return StocktakeSheet.EvidenceFailure(reason, documentRef, imageRef);
        }

        adjustment = new QuantityAdjustment
        {
            Id = Guid.CreateVersion7(),
            StockLocationId = stockLocationId,
            CatalogItemId = catalogItemId,
            Delta = delta,
            Reason = storedReason,
            DocumentRef = storedDocument,
            ImageRef = storedImage,
            Status = ApprovalStatus.Pending
        };
        return StockDecision.Accepted;
    }

    public StockDecision Approve(string? role, StockBalance balance)
    {
        if (!InventoryRoles.CanPostAdjustment(role))
        {
            return StockDecision.WrongRole;
        }

        if (Status != ApprovalStatus.Pending)
        {
            return StockDecision.NotPending;
        }

        if (!balance.TryAdjust(Delta))
        {
            return StockDecision.NegativeStock;
        }

        Status = ApprovalStatus.Approved;
        return StockDecision.Accepted;
    }

    public static StockDecision TryApplyDirect(
        string? role,
        Guid stockLocationId,
        Guid catalogItemId,
        int delta,
        string? reason,
        string? documentRef,
        string? imageRef,
        StockBalance balance,
        out QuantityAdjustment? adjustment)
    {
        adjustment = null;
        if (!InventoryRoles.CanPostAdjustment(role))
        {
            return StockDecision.WrongRole;
        }

        if (stockLocationId == Guid.Empty || catalogItemId == Guid.Empty || delta == 0)
        {
            return delta == 0 ? StockDecision.InvalidQuantity : StockDecision.Rejected;
        }

        if (!StocktakeSheet.TryEvidence(true, reason, documentRef, imageRef, out var storedReason, out var storedDocument, out var storedImage))
        {
            return StocktakeSheet.EvidenceFailure(reason, documentRef, imageRef);
        }

        if (!balance.TryAdjust(delta))
        {
            return StockDecision.NegativeStock;
        }

        adjustment = new QuantityAdjustment
        {
            Id = Guid.CreateVersion7(),
            StockLocationId = stockLocationId,
            CatalogItemId = catalogItemId,
            Delta = delta,
            Reason = storedReason,
            DocumentRef = storedDocument,
            ImageRef = storedImage,
            Status = ApprovalStatus.Approved
        };
        return StockDecision.Accepted;
    }

}
