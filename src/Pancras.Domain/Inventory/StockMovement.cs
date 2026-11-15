namespace Pancras.Domain.Inventory;

public enum StockMovementKind
{
    Receipt = 1,
    Issue = 2,
    Transfer = 3,
    Adjustment = 4
}

public sealed class StockMovement
{
    private StockMovement()
    {
    }

    public Guid Id { get; private set; }

    public Guid CatalogItemId { get; private set; }

    public Guid StockLocationId { get; private set; }

    public StockMovementKind Kind { get; private set; }

    public int Quantity { get; private set; }

    public Guid DocumentId { get; private set; }

    public DateTimeOffset OccurredAt { get; private set; }

    public static StockMovement Create(
        Guid catalogItemId,
        Guid stockLocationId,
        StockMovementKind kind,
        int quantity,
        Guid documentId,
        DateTimeOffset occurredAt)
    {
        if (catalogItemId == Guid.Empty || stockLocationId == Guid.Empty || documentId == Guid.Empty)
        {
            throw new ArgumentException("Movement references are required.");
        }

        if (quantity == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(quantity));
        }

        if (!Enum.IsDefined(kind))
        {
            throw new ArgumentOutOfRangeException(nameof(kind));
        }

        return new StockMovement
        {
            Id = Guid.CreateVersion7(),
            CatalogItemId = catalogItemId,
            StockLocationId = stockLocationId,
            Kind = kind,
            Quantity = quantity,
            DocumentId = documentId,
            OccurredAt = occurredAt
        };
    }
}
