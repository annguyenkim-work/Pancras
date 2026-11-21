using Microsoft.Extensions.Logging;
using Pancras.Domain.Employees;
using Pancras.Domain.Suppliers;

namespace Pancras.Application.Suppliers;

public sealed class ListSupplierCardsHandler(
    ISupplierRepository repository,
    ISupplierPurchaseStatsReader statsReader,
    ILogger<ListSupplierCardsHandler> logger)
{
    public async Task<SupplierCardPage> HandleAsync(
        ListSuppliersQuery query,
        EmployeeActor actor,
        CancellationToken cancellationToken)
    {
        if (!SupplierAccess.CanView(actor.Role))
        {
            logger.LogWarning("Supplier list forbidden for role {Role}.", actor.Role);
            return new SupplierCardPage([], 1, query.PageSize, 0);
        }

        var slice = await repository.ListFilteredAsync(query, query.PageNumber, query.PageSize, cancellationToken);
        var stats = await statsReader.ReadAsync(
            slice.Items.Select(supplier => supplier.Id).ToArray(),
            cancellationToken);
        logger.LogInformation("Listed {SupplierCount} suppliers.", slice.Items.Count);

        // Số tài khoản ngân hàng chỉ hiện (dạng che) cho người quản lý.
        var showBank = SupplierAccess.CanViewBank(actor.Role);
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
                showBank ? SupplierBankMask.Of(supplier.Account) : string.Empty,
                row?.PurchaseOrderCount ?? 0,
                row?.LotsNotReceived ?? 0,
                supplier.IsActive,
                Pancras.Application.Auditing.AuditStampView.From(supplier));
        }).ToArray();

        return new SupplierCardPage(cards, slice.PageNumber, query.PageSize, slice.TotalCount);
    }

    /// <summary>Các quốc gia đang có trong danh bạ, để lọc.</summary>
    public async Task<IReadOnlyList<string>> ListCountriesAsync(EmployeeActor actor, CancellationToken cancellationToken) =>
        SupplierAccess.CanView(actor.Role) ? await repository.ListCountriesAsync(cancellationToken) : [];

    public async Task<int> PageContainingAsync(string name, string code, CancellationToken cancellationToken)
    {
        var before = await repository.CountActiveBeforeAsync(name, code, cancellationToken);
        return before / Pancras.Application.Common.PageSizes.Default + 1;
    }
}
