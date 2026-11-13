using Microsoft.Extensions.Logging;

namespace Pancras.Application.Suppliers;

public sealed class ListSupplierCardsHandler(
    ISupplierRepository repository,
    ISupplierPurchaseStatsReader statsReader,
    ILogger<ListSupplierCardsHandler> logger)
{
    public Task<SupplierCardPage> HandleAsync(CancellationToken cancellationToken) =>
        HandleAsync(SupplierListFilter.From(null), SupplierPageRequest.First, cancellationToken);

    public Task<SupplierCardPage> HandleAsync(SupplierListFilter filter, CancellationToken cancellationToken) =>
        HandleAsync(filter, SupplierPageRequest.First, cancellationToken);

    public async Task<SupplierCardPage> HandleAsync(
        SupplierListFilter filter,
        SupplierPageRequest page,
        CancellationToken cancellationToken)
    {
        var slice = await repository.ListFilteredAsync(filter.Term, page.Number, page.Size, cancellationToken);
        var stats = await statsReader.ReadAsync(
            slice.Items.Select(supplier => supplier.Id).ToArray(),
            cancellationToken);
        logger.LogInformation("Listed {SupplierCount} suppliers.", slice.Items.Count);

        var cards = slice.Items.Select(supplier =>
        {
            stats.TryGetValue(supplier.Id, out var row);
            return new SupplierCard(
                supplier.Id,
                supplier.Code,
                supplier.Name,
                supplier.Country,
                supplier.ContactName,
                supplier.Phone,
                supplier.Email,
                supplier.Account,
                row?.PurchaseOrderCount ?? 0,
                row?.LotsNotReceived ?? 0,
                Pancras.Application.Auditing.AuditStampView.From(supplier));
        }).ToArray();

        return new SupplierCardPage(cards, slice.PageNumber, page.Size, slice.TotalCount);
    }

    public async Task<int> PageContainingAsync(string name, string code, CancellationToken cancellationToken)
    {
        var before = await repository.CountActiveBeforeAsync(name, code, cancellationToken);
        return before / SupplierPageRequest.DefaultSize + 1;
    }
}
