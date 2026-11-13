using Pancras.Domain.Suppliers;

namespace Pancras.Application.Suppliers;

public interface ISupplierRepository
{
    Task<IReadOnlyList<Supplier>> ListAsync(CancellationToken cancellationToken);

    Task<SupplierSlice> ListFilteredAsync(string? term, int pageNumber, int pageSize, CancellationToken cancellationToken);

    Task<Supplier?> GetActiveReadOnlyAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Tên nhà cung cấp theo mã, kể cả nhà cung cấp đã xóa, để lịch sử đơn mua luôn hiện đúng tên.</summary>
    Task<IReadOnlyDictionary<Guid, string>> GetNamesAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken);

    Task<Supplier?> GetActiveForUpdateAsync(Guid id, CancellationToken cancellationToken);

    Task<bool> NormalizedCodeExistsAsync(string normalizedCode, Guid? excludingId, CancellationToken cancellationToken);

    Task<int> CountActiveBeforeAsync(string name, string code, CancellationToken cancellationToken);

    Task<bool> TryAddAsync(Supplier supplier, CancellationToken cancellationToken);

    Task<bool> TrySaveAsync(CancellationToken cancellationToken);
}

public interface ISupplierPurchaseStatsReader
{
    Task<IReadOnlyDictionary<Guid, SupplierPurchaseStats>> ReadAsync(
        IReadOnlyCollection<Guid> supplierIds,
        CancellationToken cancellationToken);
}

public sealed record SupplierListFilter(string? Term)
{
    public static SupplierListFilter From(string? term) => new(Normalize(term));

    public bool HasCriteria => Term is not null;

    private static string? Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        return trimmed.Length == 0 ? null : trimmed;
    }
}

public sealed record SupplierPageRequest(int Number, int Size)
{
    public const int DefaultSize = 9;

    public static SupplierPageRequest First { get; } = new(1, DefaultSize);

    public static SupplierPageRequest From(int? page) =>
        new(page is > 0 ? page.Value : 1, DefaultSize);
}

public sealed record SupplierSlice(IReadOnlyList<Supplier> Items, int TotalCount, int PageNumber);

public sealed record SupplierCardPage(
    IReadOnlyList<SupplierCard> Cards,
    int PageNumber,
    int PageSize,
    int TotalCount)
{
    public int TotalPages => TotalCount == 0 ? 1 : (TotalCount + PageSize - 1) / PageSize;

    public bool HasPrevious => PageNumber > 1;

    public bool HasNext => PageNumber < TotalPages;
}

public sealed record SupplierPurchaseStats(int PurchaseOrderCount, int LotsNotReceived);

public sealed record SupplierCard(
    Guid Id,
    string Code,
    string Name,
    string Country,
    string ContactName,
    string Phone,
    string Email,
    string Account,
    int PurchaseOrderCount,
    int LotsNotReceived);

public sealed record SupplierPurchaseOption(Guid Id, string Code, string Name);

public sealed record CreatedSupplier(Guid Id, string Code, string Name);

public sealed record SupplierProfile(
    Guid Id,
    string Code,
    string Name,
    string Country,
    string ContactName,
    string Phone,
    string Email,
    string Account);

public sealed class UpdateSupplierCommand
{
    public Guid Id { get; set; }

    public string? Code { get; set; }

    public string? Name { get; set; }

    public string? Country { get; set; }

    public string? ContactName { get; set; }

    public string? Phone { get; set; }

    public string? Email { get; set; }

    public string? Account { get; set; }
}

public sealed class CreateSupplierCommand
{
    public string? Code { get; set; }

    public string? Name { get; set; }

    public string? Country { get; set; }

    public string? ContactName { get; set; }

    public string? Phone { get; set; }

    public string? Email { get; set; }

    public string? Account { get; set; }
}
