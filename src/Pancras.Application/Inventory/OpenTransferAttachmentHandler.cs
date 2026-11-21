using Pancras.Application.Auditing;
using Pancras.Application.Common;
using Pancras.Application.Purchasing;
using Pancras.Domain.Auditing;
using Pancras.Domain.Employees;

namespace Pancras.Application.Inventory;

public sealed class OpenTransferAttachmentHandler(
    IInventoryRepository inventory,
    ITransferAttachmentRepository attachments,
    IAttachmentStorage storage,
    TimeProvider? clock = null,
    IAuditLog? audit = null,
    IRequestInfo? request = null)
{
    public async Task<Result<AttachmentDownload, StockError>> HandleAsync(Guid attachmentId, EmployeeActor actor, CancellationToken cancellationToken)
    {
        var attachment = await attachments.FindAsync(attachmentId, cancellationToken);
        var slip = attachment is null ? null : await inventory.FindTransferReadOnlyAsync(attachment.TransferSlipId, cancellationToken);
        if (attachment is null || slip is null || !TransferAccess.CanTouch(actor, slip))
        {
            return Result<AttachmentDownload, StockError>.Fail(StockError.TransferNotFound);
        }

        var content = await storage.OpenReadAsync(attachment.StoredName, cancellationToken);
        if (content is null)
        {
            return Result<AttachmentDownload, StockError>.Fail(StockError.TransferNotFound);
        }

        await audit.RecordAsync(clock ?? TimeProvider.System, request, actor.Id, null, AuditActions.TransferAttachmentDownloaded, "transfer_slip", slip.Id.ToString("D"), attachment.FileName, cancellationToken);
        return Result<AttachmentDownload, StockError>.Ok(new AttachmentDownload(content, attachment.FileName, attachment.ContentType));
    }
}
