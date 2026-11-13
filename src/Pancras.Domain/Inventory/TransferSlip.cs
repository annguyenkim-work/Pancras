namespace Pancras.Domain.Inventory;

public enum TransferStatus
{
    Draft = 1,
    InTransit = 2,
    Received = 3
}

public sealed class TransferLine
{
    private TransferLine()
    {
    }

    public Guid Id { get; private set; }

    public Guid TransferSlipId { get; private set; }

    public Guid CatalogItemId { get; private set; }

    public int Quantity { get; private set; }

    internal static TransferLine Create(Guid transferSlipId, Guid catalogItemId, int quantity) =>
        new()
        {
            Id = Guid.CreateVersion7(),
            TransferSlipId = transferSlipId,
            CatalogItemId = catalogItemId,
            Quantity = quantity
        };
}

public sealed class TransferSlip
{
    public const int NoteMaxLength = 500;

    private readonly List<TransferLine> _lines = [];

    private TransferSlip()
    {
        Note = string.Empty;
    }

    public Guid Id { get; private set; }

    public Guid SourceLocationId { get; private set; }

    public Guid DestinationLocationId { get; private set; }

    public string Note { get; private set; }

    public TransferStatus Status { get; private set; }

    public IReadOnlyList<TransferLine> Lines => _lines;

    public static TransferSlip? Create(
        Guid sourceLocationId,
        Guid destinationLocationId,
        string? note,
        IReadOnlyList<(Guid CatalogItemId, int Quantity)> lines)
    {
        if (sourceLocationId == Guid.Empty
            || destinationLocationId == Guid.Empty
            || sourceLocationId == destinationLocationId)
        {
            return null;
        }

        if (!InventoryText.TryOptional(note, NoteMaxLength, out var storedNote))
        {
            return null;
        }

        if (lines is null || lines.Count == 0 || lines.Any(line => line.CatalogItemId == Guid.Empty || line.Quantity <= 0))
        {
            return null;
        }

        var slip = new TransferSlip
        {
            Id = Guid.CreateVersion7(),
            SourceLocationId = sourceLocationId,
            DestinationLocationId = destinationLocationId,
            Note = storedNote,
            Status = TransferStatus.Draft
        };
        foreach (var line in lines)
        {
            slip._lines.Add(TransferLine.Create(slip.Id, line.CatalogItemId, line.Quantity));
        }

        return slip;
    }

    public bool TryConfirmShipment(
        IReadOnlyDictionary<Guid, StockBalance> sources,
        IReadOnlyDictionary<Guid, StockBalance> destinations)
    {
        if (Status != TransferStatus.Draft || !TotalsAreShippable(sources, destinations))
        {
            return false;
        }

        foreach (var (itemId, quantity) in Totals())
        {
            if (!sources[itemId].TryShip(quantity) || !destinations[itemId].TryAddInTransit(quantity))
            {
                throw new InvalidOperationException("Shipment precheck did not hold.");
            }
        }

        Status = TransferStatus.InTransit;
        return true;
    }

    public bool TryConfirmReceipt(IReadOnlyDictionary<Guid, StockBalance> destinations)
    {
        if (Status != TransferStatus.InTransit)
        {
            return false;
        }

        foreach (var (itemId, quantity) in Totals())
        {
            if (!destinations.TryGetValue(itemId, out var destination) || destination.InTransit < quantity)
            {
                return false;
            }
        }

        foreach (var (itemId, quantity) in Totals())
        {
            if (!destinations[itemId].TryReceiveTransfer(quantity))
            {
                throw new InvalidOperationException("Receipt precheck did not hold.");
            }
        }

        Status = TransferStatus.Received;
        return true;
    }

    public void RestoreUncommitted(TransferStatus status) => Status = status;

    private bool TotalsAreShippable(
        IReadOnlyDictionary<Guid, StockBalance> sources,
        IReadOnlyDictionary<Guid, StockBalance> destinations)
    {
        foreach (var (itemId, quantity) in Totals())
        {
            if (!sources.TryGetValue(itemId, out var source) || !source.CanShip(quantity))
            {
                return false;
            }

            if (!destinations.ContainsKey(itemId))
            {
                return false;
            }
        }

        return Totals().Count > 0;
    }

    private Dictionary<Guid, int> Totals()
    {
        var totals = new Dictionary<Guid, int>();
        foreach (var line in _lines)
        {
            totals[line.CatalogItemId] = totals.GetValueOrDefault(line.CatalogItemId) + line.Quantity;
        }

        return totals;
    }
}
