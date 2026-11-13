using Microsoft.Extensions.Logging;
using Pancras.Application.Auditing;
using Pancras.Application.Common;
using Pancras.Domain.Auditing;
using Pancras.Domain.Purchasing;

namespace Pancras.Application.Purchasing;

public interface IPurchaseAttachmentRepository
{
    Task<IReadOnlyList<PurchaseAttachment>> ListAsync(Guid purchaseOrderId, CancellationToken cancellationToken);

    Task<PurchaseAttachment?> FindAsync(Guid id, CancellationToken cancellationToken);

    Task AddAsync(PurchaseAttachment attachment, CancellationToken cancellationToken);

    Task RemoveAsync(Guid id, CancellationToken cancellationToken);
}

/// <summary>Nơi lưu nội dung file chứng từ, nằm ngoài thư mục công khai của web.</summary>
public interface IAttachmentStorage
{
    Task SaveAsync(string storedName, Stream content, CancellationToken cancellationToken);

    /// <summary>Trả về null nếu file không còn.</summary>
    Task<Stream?> OpenReadAsync(string storedName, CancellationToken cancellationToken);

    Task DeleteAsync(string storedName, CancellationToken cancellationToken);
}

public sealed record PurchaseAttachmentView(Guid Id, AttachmentKind Kind, string FileName, long SizeBytes, DateTimeOffset UploadedAt, string UploadedBy);

public sealed record AttachmentDownload(Stream Content, string FileName, string ContentType);

public sealed class UploadPurchaseAttachmentCommand
{
    public Pancras.Domain.Employees.EmployeeActor Actor { get; set; } = new(Guid.Empty, null, null);

    public Guid PurchaseOrderId { get; set; }

    public AttachmentKind Kind { get; set; }

    public string? FileName { get; set; }

    public Stream? Content { get; set; }
}

public sealed class UploadPurchaseAttachmentCommandHandler(
    IPurchaseOrderRepository orders,
    IPurchaseAttachmentRepository attachments,
    IAttachmentStorage storage,
    ILogger<UploadPurchaseAttachmentCommandHandler> logger,
    TimeProvider? clock = null,
    IAuditLog? audit = null,
    IRequestInfo? request = null)
{
    public async Task<Result<Guid>> HandleAsync(UploadPurchaseAttachmentCommand command, CancellationToken cancellationToken)
    {
        if (!PurchaseAccess.CanManage(command.Actor))
        {
            return Result<Guid>.Fail(PurchaseError.Forbidden);
        }

        var now = (clock ?? TimeProvider.System).GetUtcNow();
        var cleanName = AttachmentRules.CleanFileName(command.FileName);
        if (command.Content is null || cleanName is null || !Enum.IsDefined(command.Kind))
        {
            logger.LogWarning("Attachment rejected for purchase order {PurchaseOrderId}: bad name or type.", command.PurchaseOrderId);
            return Result<Guid>.Fail(PurchaseError.AttachmentInvalid);
        }

        if (await orders.FindReadOnlyAsync(command.PurchaseOrderId, cancellationToken) is null)
        {
            return Result<Guid>.Fail(PurchaseError.InvalidOrder);
        }

        if ((await attachments.ListAsync(command.PurchaseOrderId, cancellationToken)).Count >= AttachmentRules.MaxPerOrder)
        {
            return Result<Guid>.Fail(PurchaseError.AttachmentLimit);
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
                return Result<Guid>.Fail(PurchaseError.AttachmentTooLarge);
            }
        }

        if (buffer.Length == 0)
        {
            return Result<Guid>.Fail(PurchaseError.AttachmentInvalid);
        }

        var extension = Path.GetExtension(cleanName);
        var header = buffer.GetBuffer().AsSpan(0, (int)Math.Min(buffer.Length, 8));
        if (!AttachmentRules.SignatureMatches(extension, header))
        {
            logger.LogWarning("Attachment rejected for purchase order {PurchaseOrderId}: content does not match the extension.", command.PurchaseOrderId);
            return Result<Guid>.Fail(PurchaseError.AttachmentInvalid);
        }

        var storedName = Guid.CreateVersion7().ToString("N") + extension.ToLowerInvariant();
        var attachment = PurchaseAttachment.TryCreate(command.PurchaseOrderId, command.Kind, cleanName, storedName, buffer.Length, now, request?.ActorCode);
        if (attachment is null)
        {
            return Result<Guid>.Fail(PurchaseError.AttachmentInvalid);
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
        return Result<Guid>.Ok(attachment.Id);
    }
}

public sealed class ListPurchaseAttachmentsHandler(IPurchaseAttachmentRepository attachments)
{
    public async Task<IReadOnlyList<PurchaseAttachmentView>> HandleAsync(Guid purchaseOrderId, CancellationToken cancellationToken) =>
        (await attachments.ListAsync(purchaseOrderId, cancellationToken))
            .OrderBy(item => item.UploadedAt)
            .Select(item => new PurchaseAttachmentView(item.Id, item.Kind, item.FileName, item.SizeBytes, item.UploadedAt, item.UploadedBy))
            .ToArray();
}

public sealed class OpenPurchaseAttachmentHandler(
    IPurchaseAttachmentRepository attachments,
    IAttachmentStorage storage,
    ILogger<OpenPurchaseAttachmentHandler> logger,
    TimeProvider? clock = null,
    IAuditLog? audit = null,
    IRequestInfo? request = null)
{
    public async Task<Result<AttachmentDownload>> HandleAsync(Guid attachmentId, Pancras.Domain.Employees.EmployeeActor actor, CancellationToken cancellationToken)
    {
        if (!PurchaseAccess.CanManage(actor))
        {
            return Result<AttachmentDownload>.Fail(PurchaseError.Forbidden);
        }

        var attachment = await attachments.FindAsync(attachmentId, cancellationToken);
        if (attachment is null)
        {
            return Result<AttachmentDownload>.Fail(PurchaseError.AttachmentNotFound);
        }

        var content = await storage.OpenReadAsync(attachment.StoredName, cancellationToken);
        if (content is null)
        {
            logger.LogError("Attachment {AttachmentId} has a record but no stored file.", attachment.Id);
            return Result<AttachmentDownload>.Fail(PurchaseError.AttachmentNotFound);
        }

        await audit.RecordAsync(clock ?? TimeProvider.System, request, null, null, AuditActions.PurchaseAttachmentDownloaded, "purchase_order", attachment.PurchaseOrderId.ToString("D"), attachment.FileName, cancellationToken);
        return Result<AttachmentDownload>.Ok(new AttachmentDownload(content, attachment.FileName, attachment.ContentType));
    }
}

public sealed class DeletePurchaseAttachmentCommandHandler(
    IPurchaseOrderRepository orders,
    IPurchaseAttachmentRepository attachments,
    IAttachmentStorage storage,
    ILogger<DeletePurchaseAttachmentCommandHandler> logger,
    TimeProvider? clock = null,
    IAuditLog? audit = null,
    IRequestInfo? request = null)
{
    public async Task<Result<Guid>> HandleAsync(Guid attachmentId, Pancras.Domain.Employees.EmployeeActor actor, CancellationToken cancellationToken)
    {
        if (!PurchaseAccess.CanManage(actor))
        {
            return Result<Guid>.Fail(PurchaseError.Forbidden);
        }

        var attachment = await attachments.FindAsync(attachmentId, cancellationToken);
        if (attachment is null)
        {
            return Result<Guid>.Fail(PurchaseError.AttachmentNotFound);
        }

        // Chứng từ của đơn đã nhập kho là chứng cứ kế toán: không xóa.
        var order = await orders.FindReadOnlyAsync(attachment.PurchaseOrderId, cancellationToken);
        if (order is null || !order.IsEditable)
        {
            return Result<Guid>.Fail(PurchaseError.AlreadyReceived);
        }

        await attachments.RemoveAsync(attachment.Id, cancellationToken);
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
        await audit.RecordAsync(clock ?? TimeProvider.System, request, null, null, AuditActions.PurchaseAttachmentDeleted, "purchase_order", order.Id.ToString("D"), attachment.FileName, cancellationToken);
        return Result<Guid>.Ok(attachment.Id);
    }
}
