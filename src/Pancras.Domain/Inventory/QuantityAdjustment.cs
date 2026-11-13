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
            return StockDecision.NegativeStock;
        }

        if (!StocktakeSheet.TryEvidence(true, reason, documentRef, imageRef, out var storedReason, out var storedDocument, out var storedImage))
        {
            return StockDecision.MissingEvidence;
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
            return delta == 0 ? StockDecision.NegativeStock : StockDecision.Rejected;
        }

        if (!StocktakeSheet.TryEvidence(true, reason, documentRef, imageRef, out var storedReason, out var storedDocument, out var storedImage))
        {
            return StockDecision.MissingEvidence;
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

    public void RestoreUncommitted(ApprovalStatus status) => Status = status;
}
