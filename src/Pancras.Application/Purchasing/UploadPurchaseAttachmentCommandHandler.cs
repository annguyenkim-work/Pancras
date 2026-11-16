using Microsoft.Extensions.Logging;
using Pancras.Application.Auditing;
using Pancras.Application.Common;
using Pancras.Domain.Auditing;
using Pancras.Domain.Purchasing;

namespace Pancras.Application.Purchasing;

public sealed class UploadPurchaseAttachmentCommandHandler(
    IPurchaseOrderRepository orders,
    IPurchaseAttachmentRepository attachments,
    IAttachmentStorage storage,
    ILogger<UploadPurchaseAttachmentCommandHandler> logger,
    TimeProvider? clock = null,
    IAuditLog? audit = null,
    IRequestInfo? request = null)
{
    public async Task<Result<Guid, PurchaseError>> HandleAsync(UploadPurchaseAttachmentCommand command, CancellationToken cancellationToken)
    {
        if (!PurchaseAccess.CanManage(command.Actor))
        {
            return Result<Guid, PurchaseError>.Fail(PurchaseError.Forbidden);
        }

        var now = (clock ?? TimeProvider.System).GetUtcNow();
        var cleanName = AttachmentRules.CleanFileName(command.FileName);
        if (command.Content is null || cleanName is null || !Enum.IsDefined(command.Kind))
        {
            logger.LogInformation("Attachment rejected for purchase order {PurchaseOrderId}: bad name or type.", command.PurchaseOrderId);
            return Result<Guid, PurchaseError>.Fail(PurchaseError.AttachmentInvalid);
        }

        var order = await orders.FindReadOnlyAsync(command.PurchaseOrderId, cancellationToken);
        if (order is null)
        {
            return Result<Guid, PurchaseError>.Fail(PurchaseError.InvalidOrder);
        }

        if (order.Status == PurchaseStatus.Cancelled)
        {
            return Result<Guid, PurchaseError>.Fail(PurchaseError.OrderCancelled);
        }

        if ((await attachments.ListAsync(command.PurchaseOrderId, cancellationToken)).Count >= AttachmentRules.MaxPerOrder)
        {
            return Result<Guid, PurchaseError>.Fail(PurchaseError.AttachmentLimit);
        }

        // Đọc tối đa MaxBytes + 1 byte: không tin kích thước do trình duyệt khai báo.
        var buffer = new MemoryStream();
        var chunk = new byte[81920];
        int read;
        while ((read = await command.Content.ReadAsync(chunk, cancellationToken)) > 0)
        {
            await buffer.WriteAsync(chunk.AsMemory(0, read), cancellationToken);
            if (buffer.Length > AttachmentRules.MaxBytes)
            {
                return Result<Guid, PurchaseError>.Fail(PurchaseError.AttachmentTooLarge);
            }
        }

        if (buffer.Length == 0)
        {
            return Result<Guid, PurchaseError>.Fail(PurchaseError.AttachmentInvalid);
        }

        var extension = Path.GetExtension(cleanName);
        var header = buffer.GetBuffer().AsSpan(0, (int)Math.Min(buffer.Length, 8));
        if (!AttachmentRules.SignatureMatches(extension, header))
        {
            logger.LogWarning("Attachment rejected for purchase order {PurchaseOrderId}: content does not match the extension.", command.PurchaseOrderId);
            return Result<Guid, PurchaseError>.Fail(PurchaseError.AttachmentInvalid);
        }

        var storedName = Guid.CreateVersion7().ToString("N") + extension.ToLowerInvariant();
        var attachment = PurchaseAttachment.TryCreate(command.PurchaseOrderId, command.Kind, cleanName, storedName, buffer.Length, now, request?.ActorCode);
        if (attachment is null)
        {
            return Result<Guid, PurchaseError>.Fail(PurchaseError.AttachmentInvalid);
        }

        buffer.Position = 0;
        await storage.SaveAsync(storedName, buffer, cancellationToken);
        try
        {
            await attachments.AddAsync(attachment, cancellationToken);
        }
        catch
        {
            await storage.DeleteAsync(storedName, CancellationToken.None);
            throw;
        }

        logger.LogInformation("Attached {AttachmentId} to purchase order {PurchaseOrderId}.", attachment.Id, command.PurchaseOrderId);
        await audit.RecordAsync(clock ?? TimeProvider.System, request, null, null, AuditActions.PurchaseAttachmentAdded, "purchase_order", command.PurchaseOrderId.ToString("D"), cleanName, cancellationToken);
        return Result<Guid, PurchaseError>.Ok(attachment.Id);
    }
}
