using System.Linq.Expressions;
using Pancras.Domain.Inventory;

namespace Pancras.Application.Inventory;

/// <summary>
/// Bộ lọc danh sách phiếu chuyển chạy trong database: quyền xem (<see cref="TransferWorkflowAccess.Visible"/>), nhóm "đang chờ tôi"
/// (<see cref="TransferWorkflowAccess.Waiting"/>), trạng thái, kho xuất hoặc kho nhận, từ khóa (mã CK-xxxxxx, mã hoặc tên hàng),
/// khoảng thời gian lập phiếu (theo mốc thời gian trong Id UUID v7).
/// </summary>
public sealed record TransferListQuery(
    Expression<Func<TransferSlip, bool>> Visible,
    Expression<Func<TransferSlip, bool>>? Waiting = null,
    TransferStatus? Status = null,
    Guid? LocationId = null,
    string? Term = null,
    DateTimeOffset? CreatedFrom = null,
    DateTimeOffset? CreatedBefore = null);

/// <summary>Một trang phiếu chuyển (đang xử lý trước, mới nhất trước) cùng tổng số phiếu khớp bộ lọc và còn phiếu nào xem được không.</summary>
public sealed record TransferPage(IReadOnlyList<TransferSlip> Items, int Total, int PageNumber, bool AnyVisible);

/// <summary>Bộ lọc và vị trí trang của danh sách phiếu chuyển (cỡ trang 20 hoặc 50).</summary>
public sealed record TransferListRequest(
    TransferWaitingKind? Waiting = null,
    TransferStatus? Status = null,
    Guid? LocationId = null,
    string? Term = null,
    DateTimeOffset? CreatedFrom = null,
    DateTimeOffset? CreatedBefore = null,
    int PageNumber = 1,
    int PageSize = Pancras.Application.Common.PageSizes.Default);

/// <summary>Các dòng của một trang phiếu chuyển, tổng số phiếu khớp bộ lọc, số trang đã dùng và còn phiếu nào xem được không.</summary>
public sealed record TransferListPage(IReadOnlyList<TransferSlipRow> Rows, int Total, int PageNumber, bool AnyVisible);
