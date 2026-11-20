using Microsoft.Extensions.Logging;
using Pancras.Application.Auditing;
using Pancras.Application.Common;
using Pancras.Domain.Auditing;
using Pancras.Domain.Purchasing;

namespace Pancras.Application.Purchasing;

public sealed class DeletePurchaseAttachmentCommandHandler(
    IPurchaseOrderRepository orders,
    IPurchaseAttachmentRepository attachments,
    IAttachmentStorage storage,
    IUnitOfWork unitOfWork,
    ILogger<DeletePurchaseAttachmentCommandHandler> logger,
    TimeProvider? clock = null,
    IAuditLog? audit = null,
    IRequestInfo? request = null)
{
    public async Task<Result<Guid, PurchaseError>> HandleAsync(Guid attachmentId, Pancras.Domain.Employees.EmployeeActor actor, CancellationToken cancellationToken)
    {
        if (!PurchaseAccess.CanManage(actor))
        {
            return Result<Guid, PurchaseError>.Fail(PurchaseError.Forbidden);
        }

        var attachment = await attachments.FindAsync(attachmentId, cancellationToken);
        if (attachment is null)
        {
            return Result<Guid, PurchaseError>.Fail(PurchaseError.AttachmentNotFound);
        }

        // Chứng từ của đơn đã nhập kho là chứng cứ kế toán: không xóa.
        var order = await orders.FindReadOnlyAsync(attachment.PurchaseOrderId, cancellationToken);
        if (order is null || !order.IsEditable)
        {
            return Result<Guid, PurchaseError>.Fail(PurchaseError.AlreadyReceived);
        }

        await using (var scope = await unitOfWork.BeginAsync(cancellationToken))
        {
            await attachments.RemoveAsync(attachment.Id, cancellationToken);
            audit.Stage(clock ?? TimeProvider.System, request, null, null, AuditActions.PurchaseAttachmentDeleted, "purchase_order", order.Id.ToString("D"), attachment.FileName);
            if (await scope.CommitAsync(cancellationToken) != SaveOutcome.Saved)
            {
                return Result<Guid, PurchaseError>.Fail(PurchaseError.SaveFailed);
            }
        }

        try
        {
            await storage.DeleteAsync(attachment.StoredName, cancellationToken);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Bản ghi đã xóa; file còn lại là rác trên đĩa, không làm hỏng thao tác của người dùng. Ghi log để dọn sau.
            logger.LogError(exception, "Attachment file {StoredName} could not be deleted and is now orphaned.", attachment.StoredName);
        }

        logger.LogInformation("Removed attachment {AttachmentId} from purchase order {PurchaseOrderId}.", attachment.Id, order.Id);
        return Result<Guid, PurchaseError>.Ok(attachment.Id);
    }
}
