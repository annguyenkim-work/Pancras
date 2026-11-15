using Microsoft.Extensions.Logging;
using Pancras.Application.Auditing;
using Pancras.Application.Common;
using Pancras.Application.Purchasing;
using Pancras.Domain.Auditing;
using Pancras.Domain.Employees;
using Pancras.Domain.Inventory;
using Pancras.Domain.Purchasing;

namespace Pancras.Application.Inventory;

public interface IStockDocumentAttachmentRepository
{
    Task<IReadOnlyList<StockDocumentAttachment>> ListAsync(StockDocumentKind kind, Guid documentId, CancellationToken cancellationToken);

    Task<StockDocumentAttachment?> FindAsync(Guid id, CancellationToken cancellationToken);

    Task<IReadOnlyDictionary<Guid, IReadOnlyList<StockDocumentAttachment>>> ListForDocumentsAsync(IReadOnlyCollection<Guid> documentIds, CancellationToken cancellationToken);

    Task AddAsync(StockDocumentAttachment attachment, CancellationToken cancellationToken);
}

public sealed class UploadStockDocumentAttachmentCommand
{
    public StockDocumentKind Kind { get; set; }

    public Guid DocumentId { get; set; }

    public string? FileName { get; set; }

    public Stream? Content { get; set; }

    public EmployeeActor Actor { get; set; } = new(Guid.Empty, null, null);
}

/// <summary>Tra kho của một biên bản kiểm kê, phiếu điều chỉnh hoặc phiếu hủy để kiểm quyền theo kho.</summary>
internal static class StockDocumentLookup
{
    // Chỉ đọc, không dùng FindXxxForUpdateAsync: hàm đó mở transaction giữ đến CommitAsync, mà nhánh lưu file không commit nên file bị rollback.
    public static Task<Guid?> LocationOfAsync(IInventoryRepository repository, StockDocumentKind kind, Guid documentId, CancellationToken cancellationToken) =>
        repository.FindDocumentLocationAsync(kind, documentId, cancellationToken);

    public static bool CanTouch(EmployeeActor actor, Guid locationId) =>
        InventoryAccess.CanOperate(actor.Role) && InventoryAccess.CanAccessLocation(actor, locationId);
}

/// <summary>Đính kèm file vào biên bản kiểm kê, phiếu điều chỉnh hoặc phiếu hủy (thay cho ô "đường dẫn hoặc mã ảnh" nhập tay).</summary>
public sealed class UploadStockDocumentAttachmentCommandHandler(
    IInventoryRepository inventory,
    IStockDocumentAttachmentRepository attachments,
    IAttachmentStorage storage,
    ILogger<UploadStockDocumentAttachmentCommandHandler> logger,
    TimeProvider? clock = null,
    IAuditLog? audit = null,
    IRequestInfo? request = null)
{
    /// <summary>
    /// Kiểm tra cả loạt file trước khi tạo phiếu hoặc lưu file nào (số lượng, đuôi, dung lượng, nội dung khớp đuôi),
    /// để một file sai không để lại phiếu nửa vời. Không đụng tới dữ liệu.
    /// </summary>
    public static Result<Guid> CheckFiles(IReadOnlyList<PendingTransferFile> files)
    {
        if (files.Count > AttachmentRules.MaxPerOrder)
        {
            return Result<Guid>.Fail(StockError.AttachmentLimit);
        }

        foreach (var file in files)
        {
            var cleanName = AttachmentRules.CleanFileName(file.FileName);
            if (file.Length > AttachmentRules.MaxBytes)
            {
                return Result<Guid>.Fail(StockError.AttachmentTooLarge);
            }

            if (cleanName is null || file.Length == 0 || !AttachmentRules.SignatureMatches(Path.GetExtension(cleanName), file.Header))
            {
                return Result<Guid>.Fail(StockError.AttachmentInvalid);
            }
        }

        return Result<Guid>.Ok(Guid.Empty);
    }

    public async Task<Result<Guid>> HandleAsync(UploadStockDocumentAttachmentCommand command, CancellationToken cancellationToken)
    {
        var location = await StockDocumentLookup.LocationOfAsync(inventory, command.Kind, command.DocumentId, cancellationToken);
        if (location is not Guid place || !StockDocumentLookup.CanTouch(command.Actor, place))
        {
            return Result<Guid>.Fail(StockError.Forbidden);
        }

        var cleanName = AttachmentRules.CleanFileName(command.FileName);
        if (command.Content is null || cleanName is null)
        {
            return Result<Guid>.Fail(StockError.AttachmentInvalid);
        }

        if ((await attachments.ListAsync(command.Kind, command.DocumentId, cancellationToken)).Count >= AttachmentRules.MaxPerOrder)
        {
            return Result<Guid>.Fail(StockError.AttachmentLimit);
        }

        var buffer = new MemoryStream();
        var chunk = new byte[81920];
        int read;
        while ((read = await command.Content.ReadAsync(chunk, cancellationToken)) > 0)
        {
            await buffer.WriteAsync(chunk.AsMemory(0, read), cancellationToken);
            if (buffer.Length > AttachmentRules.MaxBytes)
            {
                return Result<Guid>.Fail(StockError.AttachmentTooLarge);
            }
        }

        var extension = Path.GetExtension(cleanName);
        if (buffer.Length == 0 || !AttachmentRules.SignatureMatches(extension, buffer.GetBuffer().AsSpan(0, (int)Math.Min(buffer.Length, 8))))
        {
            logger.LogWarning("Stock document attachment rejected for {DocumentId}: empty or content does not match the extension.", command.DocumentId);
            return Result<Guid>.Fail(StockError.AttachmentInvalid);
        }

        var storedName = Guid.CreateVersion7().ToString("N") + extension.ToLowerInvariant();
        var attachment = StockDocumentAttachment.TryCreate(command.Kind, command.DocumentId, cleanName, storedName, buffer.Length, (clock ?? TimeProvider.System).GetUtcNow(), request?.ActorCode);
        if (attachment is null)
        {
            return Result<Guid>.Fail(StockError.AttachmentInvalid);
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

        await audit.RecordAsync(clock ?? TimeProvider.System, request, command.Actor.Id, null, AuditActions.StockDocumentAttachmentAdded, command.Kind.ToString().ToLowerInvariant(), command.DocumentId.ToString("D"), cleanName, cancellationToken);
        return Result<Guid>.Ok(attachment.Id);
    }
}

public sealed class OpenStockDocumentAttachmentHandler(
    IInventoryRepository inventory,
    IStockDocumentAttachmentRepository attachments,
    IAttachmentStorage storage,
    TimeProvider? clock = null,
    IAuditLog? audit = null,
    IRequestInfo? request = null)
{
    public async Task<Result<AttachmentDownload>> HandleAsync(Guid attachmentId, EmployeeActor actor, CancellationToken cancellationToken)
    {
        var attachment = await attachments.FindAsync(attachmentId, cancellationToken);
        var location = attachment is null ? null : await StockDocumentLookup.LocationOfAsync(inventory, attachment.Kind, attachment.DocumentId, cancellationToken);
        if (attachment is null || location is not Guid place || !StockDocumentLookup.CanTouch(actor, place))
        {
            return Result<AttachmentDownload>.Fail(StockError.TransferNotFound);
        }

        var content = await storage.OpenReadAsync(attachment.StoredName, cancellationToken);
        if (content is null)
        {
            return Result<AttachmentDownload>.Fail(StockError.TransferNotFound);
        }

        await audit.RecordAsync(clock ?? TimeProvider.System, request, actor.Id, null, AuditActions.StockDocumentAttachmentDownloaded, attachment.Kind.ToString().ToLowerInvariant(), attachment.DocumentId.ToString("D"), attachment.FileName, cancellationToken);
        return Result<AttachmentDownload>.Ok(new AttachmentDownload(content, attachment.FileName, attachment.ContentType));
    }
}
