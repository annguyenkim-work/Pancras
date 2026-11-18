using Microsoft.Extensions.Logging;
using Pancras.Application.Auditing;
using Pancras.Application.Common;
using Pancras.Application.Purchasing;
using Pancras.Domain.Auditing;
using Pancras.Domain.Employees;
using Pancras.Domain.Inventory;
using Pancras.Domain.Purchasing;

namespace Pancras.Application.Inventory;

public sealed record TransferAttachmentView(Guid Id, string FileName, long SizeBytes, DateTimeOffset UploadedAt, string UploadedBy);

/// <summary>Ai được thao tác với một phiếu chuyển: Admin/Giám đốc/Kế toán, hoặc nhân viên kho ở kho nguồn hay kho đích.</summary>
public static class TransferAccess
{
    public static bool CanTouch(EmployeeActor actor, TransferSlip slip) =>
        InventoryAccess.CanOperate(actor.Role)
        && (InventoryAccess.CanAccessLocation(actor, slip.SourceLocationId) || InventoryAccess.CanAccessLocation(actor, slip.DestinationLocationId));

    /// <summary>
    /// In phiếu xuất kho: tài chính in mọi phiếu; Thủ kho, Trưởng kho chỉ in phiếu của kho xuất (kho nhận xem được nhưng không in).
    /// Phiếu phải từ đã duyệt trở đi (<see cref="TransferSlip.CanPrint(TransferStatus)"/>).
    /// </summary>
    public static bool CanPrint(EmployeeActor actor, TransferSlip slip) =>
        CanPrint(actor, slip.Status, slip.SourceLocationId);

    /// <summary>Cùng quy tắc in theo trạng thái và kho xuất, để bảng hướng dẫn tính quyền mà không cần dựng phiếu.</summary>
    public static bool CanPrint(EmployeeActor actor, TransferStatus status, Guid sourceLocationId) =>
        TransferSlip.CanPrint(status)
        && InventoryAccess.CanOperate(actor.Role)
        && InventoryAccess.CanAccessLocation(actor, sourceLocationId);

    /// <summary>
    /// Người lập phiếu: chỉ người này được sửa hoặc hủy phiếu (người duyệt chỉ duyệt hoặc từ chối).
    /// Phiếu cũ chưa ghi người lập thì giữ quy tắc trước đây (nhân viên kho nguồn hoặc kho đích).
    /// </summary>
    public static bool IsCreator(EmployeeActor actor, TransferSlip slip) =>
        slip.RequestedById == Guid.Empty || slip.RequestedById == actor.Id;
}

/// <summary>File người dùng vừa chọn, chưa lưu: tên, dung lượng và vài byte đầu để đối chiếu đuôi file.</summary>
public sealed record PendingTransferFile(string? FileName, long Length, byte[] Header);

public sealed class UploadTransferAttachmentCommand
{
    public Guid TransferSlipId { get; set; }

    public string? FileName { get; set; }

    public Stream? Content { get; set; }

    public EmployeeActor Actor { get; set; } = new(Guid.Empty, null, null);
}
