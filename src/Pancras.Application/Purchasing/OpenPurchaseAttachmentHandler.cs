using Microsoft.Extensions.Logging;
using Pancras.Application.Auditing;
using Pancras.Application.Common;
using Pancras.Domain.Auditing;
using Pancras.Domain.Purchasing;

namespace Pancras.Application.Purchasing;

/// <summary>Tải chứng từ: vai trò quản lý tải mọi file; kho tải mọi file của đơn mà mình được xem (đơn đã duyệt trở đi của kho mình).</summary>
public sealed class OpenPurchaseAttachmentHandler(
    IPurchaseOrderRepository orders,
    IPurchaseAttachmentRepository attachments,
    IAttachmentStorage storage,
    ILogger<OpenPurchaseAttachmentHandler> logger,
    TimeProvider? clock = null,
    IAuditLog? audit = null,
    IRequestInfo? request = null)
{
    public async Task<Result<AttachmentDownload, PurchaseError>> HandleAsync(Guid attachmentId, Pancras.Domain.Employees.EmployeeActor actor, CancellationToken cancellationToken)
    {
        if (!PurchaseAccess.CanManage(actor) && !PurchaseAccess.IsWarehouseStaff(actor))
        {
            return Result<AttachmentDownload, PurchaseError>.Fail(PurchaseError.Forbidden);
        }

        var attachment = await attachments.FindAsync(attachmentId, cancellationToken);
        if (attachment is null)
        {
            return Result<AttachmentDownload, PurchaseError>.Fail(PurchaseError.AttachmentNotFound);
        }

        if (!PurchaseAccess.CanManage(actor))
        {
            // Kho: chỉ tải file của đơn mình được xem. Không phân biệt "không có" với "không được xem" để không lộ đơn.
            var order = await orders.FindReadOnlyAsync(attachment.PurchaseOrderId, cancellationToken);
            if (order is null || !PurchaseAccess.CanView(actor, order.ReceiveLocationId, order.Status))
            {
                return Result<AttachmentDownload, PurchaseError>.Fail(PurchaseError.AttachmentNotFound);
            }
        }

        var content = await storage.OpenReadAsync(attachment.StoredName, cancellationToken);
        if (content is null)
        {
            logger.LogError("Attachment {AttachmentId} has a record but no stored file.", attachment.Id);
            return Result<AttachmentDownload, PurchaseError>.Fail(PurchaseError.AttachmentNotFound);
        }

        await audit.RecordAsync(clock ?? TimeProvider.System, request, null, null, AuditActions.PurchaseAttachmentDownloaded, "purchase_order", attachment.PurchaseOrderId.ToString("D"), attachment.FileName, cancellationToken);
        return Result<AttachmentDownload, PurchaseError>.Ok(new AttachmentDownload(content, attachment.FileName, attachment.ContentType));
    }
}
