using Pancras.Application.Common;
using Pancras.Application.Auditing;
using Microsoft.Extensions.Logging;
using Pancras.Domain.Employees;
using Pancras.Domain.Inventory;

namespace Pancras.Application.Locations;

/// <summary>Sửa và xóa mềm kho/showroom (tách khỏi <see cref="ILocationRepository"/> để không ảnh hưởng nơi chỉ cần đọc).</summary>
public interface ILocationManagementRepository
{
    Task<StockLocation?> GetForUpdateAsync(Guid id, CancellationToken cancellationToken);

    Task<bool> NameExistsAsync(string name, Guid excludingId, CancellationToken cancellationToken);

    Task<bool> HasStockAsync(Guid id, CancellationToken cancellationToken);

    Task<bool> HasActiveEmployeesAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Còn đơn mua (nháp, chờ duyệt, đã duyệt, đang về, nhận một phần) nhận về kho này hoặc phiếu chuyển chưa đóng có kho này là kho xuất hoặc kho nhận.</summary>
    Task<bool> HasOpenDocumentsAsync(Guid id, CancellationToken cancellationToken);

    Task<SaveOutcome> TrySaveAsync(CancellationToken cancellationToken);
}
