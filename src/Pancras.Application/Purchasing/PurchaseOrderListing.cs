using System.Linq.Expressions;
using Pancras.Domain.Purchasing;

namespace Pancras.Application.Purchasing;

/// <summary>
/// Bộ lọc danh sách đơn mua chạy trong database: quyền xem (<see cref="PurchaseAccess.Visible"/>), nhà cung cấp, trạng thái và từ khóa
/// (mã đơn, nhà cung cấp, số container, B/L, cảng).
/// </summary>
public sealed record PurchaseOrderQuery(
    Expression<Func<PurchaseOrder, bool>> Visible,
    Guid? SupplierId = null,
    string? Term = null,
    PurchaseStatus? Status = null);

/// <summary>
/// Một trang đơn mua (mới nhất trước) cùng tổng số đơn khớp bộ lọc, số trang đã dùng, còn đơn nào xem được không (bỏ qua
/// nhà cung cấp và từ khóa) và Id đơn đầu tiên của cả danh sách (đơn mở mặc định).
/// </summary>
public sealed record PurchaseOrderPage(
    IReadOnlyList<PurchaseOrder> Items,
    int Total,
    int PageNumber,
    bool AnyVisible,
    Guid? FirstId);

/// <summary>Mã, số container, vận đơn và trạng thái của một đơn: đủ để tìm đơn trùng lô hàng mà không nạp cả đơn.</summary>
public sealed record PurchaseShipmentKey(Guid Id, string Code, string ContainerNumber, string BillOfLading, PurchaseStatus Status);
