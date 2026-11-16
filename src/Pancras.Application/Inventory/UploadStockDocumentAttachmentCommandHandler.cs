using Microsoft.Extensions.Logging;
using Pancras.Application.Auditing;
using Pancras.Application.Common;
using Pancras.Application.Purchasing;
using Pancras.Domain.Auditing;
using Pancras.Domain.Employees;
using Pancras.Domain.Inventory;
using Pancras.Domain.Purchasing;

namespace Pancras.Application.Inventory;

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
    public static Result<Guid, StockError> CheckFiles(IReadOnlyList<PendingTransferFile> files)
    {
        if (files.Count > AttachmentRules.MaxPerOrder)
        {
            return Result<Guid, StockError>.Fail(StockError.AttachmentLimit);
        }

        foreach (var file in files)
        {
            var cleanName = AttachmentRules.CleanFileName(file.FileName);
            if (file.Length > AttachmentRules.MaxBytes)
            {
                return Result<Guid, StockError>.Fail(StockError.AttachmentTooLarge);
            }

            if (cleanName is null || file.Length == 0 || !AttachmentRules.SignatureMatches(Path.GetExtension(cleanName), file.Header))
            {
                return Result<Guid, StockError>.Fail(StockError.AttachmentInvalid);
            }
        }

        return Result<Guid, StockError>.Ok(Guid.Empty);
    }

    public async Task<Result<Guid, StockError>> HandleAsync(UploadStockDocumentAttachmentCommand command, CancellationToken cancellationToken)
    {
        var location = await StockDocumentLookup.LocationOfAsync(inventory, command.Kind, command.DocumentId, cancellationToken);
        if (location is not Guid place || !StockDocumentLookup.CanTouch(command.Actor, place))
        {
            return Result<Guid, StockError>.Fail(StockError.Forbidden);
        }

        var cleanName = AttachmentRules.CleanFileName(command.FileName);
        if (command.Content is null || cleanName is null)
        {
            return Result<Guid, StockError>.Fail(StockError.AttachmentInvalid);
        }

        if ((await attachments.ListAsync(command.Kind, command.DocumentId, cancellationToken)).Count >= AttachmentRules.MaxPerOrder)
        {
            return Result<Guid, StockError>.Fail(StockError.AttachmentLimit);
        }

        var buffer = new MemoryStream();
        var chunk = new byte[81920];
        int read;
        while ((read = await command.Content.ReadAsync(chunk, cancellationToken)) > 0)
        {
            await buffer.WriteAsync(chunk.AsMemory(0, read), cancellationToken);
            if (buffer.Length > AttachmentRules.MaxBytes)
            {
                return Result<Guid, StockError>.Fail(StockError.AttachmentTooLarge);
            }
        }

        var extension = Path.GetExtension(cleanName);
        if (buffer.Length == 0 || !AttachmentRules.SignatureMatches(extension, buffer.GetBuffer().AsSpan(0, (int)Math.Min(buffer.Length, 8))))
        {
            logger.LogWarning("Stock document attachment rejected for {DocumentId}: empty or content does not match the extension.", command.DocumentId);
            return Result<Guid, StockError>.Fail(StockError.AttachmentInvalid);
        }

        var storedName = Guid.CreateVersion7().ToString("N") + extension.ToLowerInvariant();
        var attachment = StockDocumentAttachment.TryCreate(command.Kind, command.DocumentId, cleanName, storedName, buffer.Length, (clock ?? TimeProvider.System).GetUtcNow(), request?.ActorCode);
        if (attachment is null)
        {
            return Result<Guid, StockError>.Fail(StockError.AttachmentInvalid);
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
        return Result<Guid, StockError>.Ok(attachment.Id);
    }
}
