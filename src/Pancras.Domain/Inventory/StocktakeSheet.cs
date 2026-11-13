namespace Pancras.Domain.Inventory;

public sealed class StocktakeSheet
{
    public const int ReasonMaxLength = 500;

    public const int DocumentRefMaxLength = 200;

    public const int ImageRefMaxLength = 500;

    private StocktakeSheet()
    {
        Reason = string.Empty;
        DocumentRef = string.Empty;
        ImageRef = string.Empty;
        CreatedByRole = string.Empty;
    }

    public Guid Id { get; private set; }

    public Guid StockLocationId { get; private set; }

    public Guid CatalogItemId { get; private set; }

    public int BookQuantity { get; private set; }

    public int CountedQuantity { get; private set; }

    public string Reason { get; private set; }

    public string DocumentRef { get; private set; }

    public string ImageRef { get; private set; }

    public string CreatedByRole { get; private set; }

    public ApprovalStatus Status { get; private set; }

    public int Variance => CountedQuantity - BookQuantity;

    public static StockDecision TryCreate(
        string? role,
        Guid stockLocationId,
        Guid catalogItemId,
        int bookQuantity,
        int countedQuantity,
        string? reason,
        string? documentRef,
        string? imageRef,
        out StocktakeSheet? sheet)
    {
        sheet = null;
        if (!InventoryRoles.CanCountStock(role))
        {
            return StockDecision.WrongRole;
        }

        if (stockLocationId == Guid.Empty || catalogItemId == Guid.Empty || bookQuantity < 0 || countedQuantity < 0)
        {
            return StockDecision.Rejected;
        }

        if (!TryEvidence(countedQuantity != bookQuantity, reason, documentRef, imageRef, out var storedReason, out var storedDocument, out var storedImage))
        {
            return StockDecision.MissingEvidence;
        }

        sheet = new StocktakeSheet
        {
            Id = Guid.CreateVersion7(),
            StockLocationId = stockLocationId,
            CatalogItemId = catalogItemId,
            BookQuantity = bookQuantity,
            CountedQuantity = countedQuantity,
            Reason = storedReason,
            DocumentRef = storedDocument,
            ImageRef = storedImage,
            Status = InventoryRoles.IsFinance(role) ? ApprovalStatus.Approved : ApprovalStatus.Pending,
            CreatedByRole = role ?? string.Empty
        };
        return StockDecision.Accepted;
    }

    public StockDecision Approve(string? role)
    {
        if (!InventoryRoles.IsShowroomLead(role) && !InventoryRoles.IsFinance(role))
        {
            return StockDecision.WrongRole;
        }

        if (Status != ApprovalStatus.Pending)
        {
            return StockDecision.NotPending;
        }

        Status = ApprovalStatus.Approved;
        return StockDecision.Accepted;
    }

    public void RestoreUncommitted(ApprovalStatus status) => Status = status;

    internal static bool TryEvidence(
        bool required,
        string? reason,
        string? documentRef,
        string? imageRef,
        out string storedReason,
        out string storedDocument,
        out string storedImage)
    {
        storedReason = string.Empty;
        storedDocument = string.Empty;
        storedImage = string.Empty;
        if (!InventoryText.TryOptional(imageRef, ImageRefMaxLength, out storedImage))
        {
            return false;
        }

        if (required)
        {
            return InventoryText.TryRequired(reason, ReasonMaxLength, out storedReason)
                && InventoryText.TryRequired(documentRef, DocumentRefMaxLength, out storedDocument);
        }

        return InventoryText.TryOptional(reason, ReasonMaxLength, out storedReason)
            && InventoryText.TryOptional(documentRef, DocumentRefMaxLength, out storedDocument);
    }
}
