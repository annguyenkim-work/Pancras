using Pancras.Domain.Employees;
using Pancras.Domain.Suppliers;

namespace Pancras.Application.Suppliers;

public interface ISupplierRepository
{
    /// <summary>Nhà cung cấp đang hợp tác, để chọn khi lập đơn mua.</summary>
    Task<IReadOnlyList<Supplier>> ListAsync(CancellationToken cancellationToken);

    Task<SupplierSlice> ListFilteredAsync(SupplierListFilter filter, int pageNumber, int pageSize, CancellationToken cancellationToken);

    /// <summary>Các quốc gia đang có trong danh bạ (mọi trạng thái), để lọc.</summary>
    Task<IReadOnlyList<string>> ListCountriesAsync(CancellationToken cancellationToken);

    Task<Supplier?> GetActiveReadOnlyAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Theo mã, kể cả nhà cung cấp đã ngừng hợp tác.</summary>
    Task<Supplier?> GetAnyReadOnlyAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Tên nhà cung cấp theo mã, kể cả nhà cung cấp đã ngừng hợp tác, để lịch sử đơn mua luôn hiện đúng tên.</summary>
    Task<IReadOnlyDictionary<Guid, string>> GetNamesAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken);

    Task<Supplier?> GetActiveForUpdateAsync(Guid id, CancellationToken cancellationToken);

    Task<Supplier?> GetAnyForUpdateAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Mã đã được nhà cung cấp đang hợp tác khác dùng chưa.</summary>
    Task<bool> NormalizedCodeExistsAsync(string normalizedCode, Guid? excludingId, CancellationToken cancellationToken);

    /// <summary>Tên nhà cung cấp đã ngừng hợp tác đang giữ mã này, hoặc null.</summary>
    Task<string?> InactiveNameWithCodeAsync(string normalizedCode, CancellationToken cancellationToken);

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

public enum SupplierStatusFilter
{
    Active = 0,
    Inactive = 1,
    All = 2
}

public sealed record SupplierListFilter(string? Term, SupplierStatusFilter Status = SupplierStatusFilter.Active, string? Country = null)
{
    public static SupplierListFilter From(string? term, SupplierStatusFilter status = SupplierStatusFilter.Active, string? country = null) =>
        new(Normalize(term), Enum.IsDefined(status) ? status : SupplierStatusFilter.Active, Normalize(country));

    public bool HasCriteria => Term is not null || Status != SupplierStatusFilter.Active || Country is not null;

    private static string? Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Trim();
    }
}

public sealed record SupplierPageRequest(int Number, int Size)
{
    public const int DefaultSize = 10;

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

/// <summary>Một dòng danh bạ. Tài khoản ngân hàng chỉ có (dạng che) cho người có quyền quản lý.</summary>
public sealed record SupplierCard(
    Guid Id,
    string Code,
    string Name,
    string Country,
    string ContactName,
    string Phone,
    string Email,
    string AccountMask,
    int PurchaseOrderCount,
    int LotsNotReceived,
    bool IsActive = true,
    Pancras.Application.Auditing.AuditStampView? Stamp = null);

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
    string BankName,
    string Account,
    string SwiftCode,
    int PurchaseOrderCount = 0,
    int LotsNotReceived = 0,
    int Version = 0);

public sealed class UpdateSupplierCommand
{
    public Guid Id { get; set; }

    /// <summary>Phiên bản hồ sơ lúc mở form; khác phiên bản hiện tại nghĩa là có người sửa trước.</summary>
    public int Version { get; set; }

    public string? Code { get; set; }

    public string? Name { get; set; }

    public string? Country { get; set; }

    public string? ContactName { get; set; }

    public string? Phone { get; set; }

    public string? Email { get; set; }

    public string? BankName { get; set; }

    public string? Account { get; set; }

    public string? SwiftCode { get; set; }

    /// <summary>Người thao tác. Lớp Web gán sau khi nhận form, không lấy từ dữ liệu gửi lên.</summary>
    public EmployeeActor Actor { get; set; } = new(Guid.Empty, null, null);
}

public sealed class CreateSupplierCommand
{
    public string? Code { get; set; }

    public string? Name { get; set; }

    public string? Country { get; set; }

    public string? ContactName { get; set; }

    public string? Phone { get; set; }

    public string? Email { get; set; }

    public string? BankName { get; set; }

    public string? Account { get; set; }

    public string? SwiftCode { get; set; }

    public EmployeeActor Actor { get; set; } = new(Guid.Empty, null, null);
}

public static class SupplierBankMask
{
    /// <summary>Che số tài khoản, chỉ giữ 4 ký tự cuối. Rỗng thì trả về rỗng.</summary>
    public static string Of(string? account)
    {
        var value = account?.Trim() ?? string.Empty;
        if (value.Length == 0)
        {
            return string.Empty;
        }

        return value.Length <= 4 ? "••••" : "••••" + value[^4..];
    }
}
