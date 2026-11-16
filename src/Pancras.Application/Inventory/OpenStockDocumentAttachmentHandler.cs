using Microsoft.Extensions.Logging;
using Pancras.Application.Auditing;
using Pancras.Application.Common;
using Pancras.Application.Purchasing;
using Pancras.Domain.Auditing;
using Pancras.Domain.Employees;
using Pancras.Domain.Inventory;
using Pancras.Domain.Purchasing;

namespace Pancras.Application.Inventory;

public sealed class OpenStockDocumentAttachmentHandler(
    IInventoryRepository inventory,
    IStockDocumentAttachmentRepository attachments,
    IAttachmentStorage storage,
    TimeProvider? clock = null,
    IAuditLog? audit = null,
    IRequestInfo? request = null)
{
    public async Task<Result<AttachmentDownload, StockError>> HandleAsync(Guid attachmentId, EmployeeActor actor, CancellationToken cancellationToken)
    {
        var attachment = await attachments.FindAsync(attachmentId, cancellationToken);
        var location = attachment is null ? null : await StockDocumentLookup.LocationOfAsync(inventory, attachment.Kind, attachment.DocumentId, cancellationToken);
        if (attachment is null || location is not Guid place || !StockDocumentLookup.CanTouch(actor, place))
        {
            return Result<AttachmentDownload, StockError>.Fail(StockError.TransferNotFound);
        }

        var content = await storage.OpenReadAsync(attachment.StoredName, cancellationToken);
        if (content is null)
        {
            return Result<AttachmentDownload, StockError>.Fail(StockError.TransferNotFound);
        }

        await audit.RecordAsync(clock ?? TimeProvider.System, request, actor.Id, null, AuditActions.StockDocumentAttachmentDownloaded, attachment.Kind.ToString().ToLowerInvariant(), attachment.DocumentId.ToString("D"), attachment.FileName, cancellationToken);
        return Result<AttachmentDownload, StockError>.Ok(new AttachmentDownload(content, attachment.FileName, attachment.ContentType));
    }
}
