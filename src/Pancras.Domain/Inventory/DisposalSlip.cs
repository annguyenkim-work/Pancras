namespace Pancras.Domain.Inventory;

public sealed class DisposalSlip : Pancras.Domain.Auditing.AuditStamped
{
    private DisposalSlip()
    {
        Reason = string.Empty;
        DocumentRef = string.Empty;
        ImageRef = string.Empty;
        CreatedByRole = string.Empty;
    }

    public Guid Id { get; private set; }

    public Guid StockLocationId { get; private set; }

    public Guid CatalogItemId { get; private set; }

    public string Reason { get; private set; }

    public string DocumentRef { get; private set; }

    public string ImageRef { get; private set; }

    public string CreatedByRole { get; private set; }

    public ApprovalStatus Status { get; private set; }

    public static StockDecision TryCreate(
        Guid stockLocationId,
        Guid catalogItemId,
        string? reason,
        string? documentRef,
        string? imageRef,
        string? createdByRole,
        out DisposalSlip? slip)
    {
        slip = null;
        if (stockLocationId == Guid.Empty || catalogItemId == Guid.Empty)
        {
            return StockDecision.Rejected;
        }

        if (!StocktakeSheet.TryEvidence(true, reason, documentRef, imageRef, out var storedReason, out var storedDocument, out var storedImage))
        {
            return StockDecision.MissingEvidence;
        }

        slip = new DisposalSlip
        {
            Id = Guid.CreateVersion7(),
            StockLocationId = stockLocationId,
            CatalogItemId = catalogItemId,
            Reason = storedReason,
            DocumentRef = storedDocument,
            ImageRef = storedImage,
            Status = ApprovalStatus.Pending,
            CreatedByRole = createdByRole ?? string.Empty
        };
        return StockDecision.Accepted;
    }

    public StockDecision Approve(string? role, StockBalance balance)
    {
        if (!InventoryRoles.IsShowroomLead(role) && !InventoryRoles.IsFinance(role))
        {
            return StockDecision.WrongRole;
        }

        if (Status != ApprovalStatus.Pending)
        {
            return StockDecision.NotPending;
        }

        if (!balance.TryDisposeOne())
        {
            return StockDecision.NegativeStock;
        }

        Status = ApprovalStatus.Approved;
        return StockDecision.Accepted;
    }

    public void RestoreUncommitted(ApprovalStatus status) => Status = status;
}
