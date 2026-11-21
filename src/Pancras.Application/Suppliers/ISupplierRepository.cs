using Pancras.Domain.Suppliers;

namespace Pancras.Application.Suppliers;

public interface ISupplierRepository
{
    /// <summary>Nhà cung cấp đang hợp tác, để chọn khi lập đơn mua.</summary>
    Task<IReadOnlyList<Supplier>> ListAsync(CancellationToken cancellationToken);

    Task<SupplierSlice> ListFilteredAsync(ListSuppliersQuery filter, int pageNumber, int pageSize, CancellationToken cancellationToken);

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

    Task AddAsync(Supplier supplier, CancellationToken cancellationToken);
}
