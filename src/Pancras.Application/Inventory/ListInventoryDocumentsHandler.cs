using Microsoft.Extensions.Logging;
using Pancras.Domain.Inventory;

namespace Pancras.Application.Inventory;

public sealed class ListInventoryDocumentsHandler(
    IInventoryRepository repository,
    ILogger<ListInventoryDocumentsHandler> logger)
{
    public async Task<IReadOnlyList<StockLocationChoice>> ListLocationsAsync(CancellationToken cancellationToken)
    {
        var locations = await repository.ListLocationsAsync(cancellationToken);
        return locations
            .OrderBy(location => location.Name, StringComparer.Ordinal)
            .Select(location => new StockLocationChoice(location.Id, location.Name, location.Kind))
            .ToArray();
    }

    public async Task<IReadOnlyList<StockItemChoice>> ListItemsAsync(CancellationToken cancellationToken)
    {
        var items = await repository.ListItemChoicesAsync(cancellationToken);
        return items.OrderBy(item => item.Name, StringComparer.Ordinal).ThenBy(item => item.Code, StringComparer.Ordinal).ToArray();
    }

    public async Task<IReadOnlyList<TransferSlipRow>> ListTransfersAsync(CancellationToken cancellationToken)
    {
        var slips = await repository.ListTransfersAsync(cancellationToken);
        var names = await NamesAsync(cancellationToken);
        logger.LogInformation("Listed {TransferSlipCount} transfer slips.", slips.Count);
        return slips
            .OrderBy(slip => slip.Status == TransferStatus.InTransit ? 0 : slip.Status == TransferStatus.Draft ? 1 : 2)
            .ThenByDescending(slip => slip.Id)
            .Select(slip => new TransferSlipRow(
            slip.Id,
            names.Location(slip.SourceLocationId),
            names.Location(slip.DestinationLocationId),
            slip.Note,
            slip.Status,
            slip.Lines.Select(line => new TransferLineRow(
                line.CatalogItemId,
                names.Code(line.CatalogItemId),
                names.Item(line.CatalogItemId),
                line.Quantity,
                names.Unit(line.CatalogItemId))).ToArray()))
            .ToArray();
    }

    public async Task<IReadOnlyList<StocktakeRow>> ListStocktakesAsync(CancellationToken cancellationToken)
    {
        var sheets = await repository.ListStocktakesAsync(cancellationToken);
        var names = await NamesAsync(cancellationToken);
        return sheets.Select(sheet => new StocktakeRow(
            sheet.Id,
            names.Location(sheet.StockLocationId),
            names.Code(sheet.CatalogItemId),
            names.Item(sheet.CatalogItemId),
            sheet.BookQuantity,
            sheet.CountedQuantity,
            sheet.Variance,
            sheet.Reason,
            sheet.DocumentRef,
            sheet.Status,
            names.Unit(sheet.CatalogItemId))).ToArray();
    }

    public async Task<IReadOnlyList<AdjustmentRow>> ListAdjustmentsAsync(CancellationToken cancellationToken)
    {
        var adjustments = await repository.ListAdjustmentsAsync(cancellationToken);
        var names = await NamesAsync(cancellationToken);
        return adjustments.Select(adjustment => new AdjustmentRow(
            adjustment.Id,
            names.Location(adjustment.StockLocationId),
            names.Code(adjustment.CatalogItemId),
            names.Item(adjustment.CatalogItemId),
            adjustment.Delta,
            adjustment.Reason,
            adjustment.DocumentRef,
            adjustment.Status)).ToArray();
    }

    public async Task<IReadOnlyList<DisposalRow>> ListDisposalsAsync(CancellationToken cancellationToken)
    {
        var slips = await repository.ListDisposalsAsync(cancellationToken);
        var names = await NamesAsync(cancellationToken);
        return slips.Select(slip => new DisposalRow(
            slip.Id,
            names.Location(slip.StockLocationId),
            names.Code(slip.CatalogItemId),
            names.Item(slip.CatalogItemId),
            slip.Reason,
            slip.DocumentRef,
            slip.Status)).ToArray();
    }

    private async Task<NameMap> NamesAsync(CancellationToken cancellationToken)
    {
        var locations = await repository.ListLocationsAsync(cancellationToken);
        var items = await repository.ListItemChoicesAsync(cancellationToken);
        return new NameMap(locations, items);
    }

    private sealed class NameMap
    {
        private readonly Dictionary<Guid, string> _locations;
        private readonly Dictionary<Guid, StockItemChoice> _items;

        public NameMap(IReadOnlyList<StockLocation> locations, IReadOnlyList<StockItemChoice> items)
        {
            _locations = locations.ToDictionary(location => location.Id, location => location.Name);
            _items = items.ToDictionary(item => item.Id);
        }

        public string Location(Guid id) => _locations.TryGetValue(id, out var name) ? name : string.Empty;

        public string Code(Guid id) => _items.TryGetValue(id, out var item) ? item.Code : string.Empty;

        public string Item(Guid id) => _items.TryGetValue(id, out var item) ? item.Name : string.Empty;

        public string Unit(Guid id) => _items.TryGetValue(id, out var item) ? item.Unit : string.Empty;
    }
}
