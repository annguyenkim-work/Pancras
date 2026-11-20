using Microsoft.Extensions.Logging;
using Pancras.Application.Auditing;
using Pancras.Application.Common;
using Pancras.Application.Purchasing;
using Pancras.Domain.Auditing;
using Pancras.Domain.Employees;
using Pancras.Domain.Inventory;
using Pancras.Domain.Purchasing;

namespace Pancras.Application.Inventory;

public sealed class UploadTransferAttachmentCommandHandler(
    IInventoryRepository inventory,
    ITransferAttachmentRepository attachments,
    IAttachmentStorage storage,
    IUnitOfWork unitOfWork,
    ILogger<UploadTransferAttachmentCommandHandler> logger,
    TimeProvider? clock = null,
    IAuditLog? audit = null,
    IRequestInfo? request = null)
{
    /// <summary>
    /// Kiểm tra cả loạt file trước khi lưu file nào (quyền, số lượng, đuôi, dung lượng, nội dung khớp đuôi),
    /// để một file sai không để lại các file đứng trước nó.
    /// </summary>
    public async Task<Result<Guid, StockError>> CheckAsync(Guid transferSlipId, IReadOnlyList<PendingTransferFile> files, EmployeeActor actor, CancellationToken cancellationToken)
    {
        var slip = await inventory.FindTransferReadOnlyAsync(transferSlipId, cancellationToken);
        if (slip is null || !TransferAccess.CanTouch(actor, slip))
        {
            return Result<Guid, StockError>.Fail(StockError.Forbidden);
        }

        if (slip.Status is TransferStatus.Cancelled or TransferStatus.Rejected)
        {
            return Result<Guid, StockError>.Fail(StockError.TransferStateChanged);
        }

        if (files.Count == 0)
        {
            return Result<Guid, StockError>.Ok(slip.Id);
        }

        if ((await attachments.ListAsync(slip.Id, cancellationToken)).Count + files.Count > AttachmentRules.MaxPerOrder)
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

            if (cleanName is null
                || file.Length == 0
                || !AttachmentRules.SignatureMatches(Path.GetExtension(cleanName), file.Header))
            {
                return Result<Guid, StockError>.Fail(StockError.AttachmentInvalid);
            }
        }

        return Result<Guid, StockError>.Ok(slip.Id);
    }

    public async Task<Result<Guid, StockError>> HandleAsync(UploadTransferAttachmentCommand command, CancellationToken cancellationToken)
    {
        var slip = await inventory.FindTransferReadOnlyAsync(command.TransferSlipId, cancellationToken);
        if (slip is null || !TransferAccess.CanTouch(command.Actor, slip))
        {
            return Result<Guid, StockError>.Fail(StockError.Forbidden);
        }

        if (slip.Status is TransferStatus.Cancelled or TransferStatus.Rejected)
        {
            return Result<Guid, StockError>.Fail(StockError.TransferStateChanged);
        }

        var cleanName = AttachmentRules.CleanFileName(command.FileName);
        if (command.Content is null || cleanName is null)
        {
            return Result<Guid, StockError>.Fail(StockError.AttachmentInvalid);
        }

        if ((await attachments.ListAsync(slip.Id, cancellationToken)).Count >= AttachmentRules.MaxPerOrder)
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
            logger.LogWarning("Transfer attachment rejected for {TransferSlipId}: empty or content does not match the extension.", slip.Id);
            return Result<Guid, StockError>.Fail(StockError.AttachmentInvalid);
        }

        var storedName = Guid.CreateVersion7().ToString("N") + extension.ToLowerInvariant();
        var attachment = TransferAttachment.TryCreate(slip.Id, cleanName, storedName, buffer.Length, (clock ?? TimeProvider.System).GetUtcNow(), request?.ActorCode);
        if (attachment is null)
        {
            return Result<Guid, StockError>.Fail(StockError.AttachmentInvalid);
        }

        buffer.Position = 0;
        await storage.SaveAsync(storedName, buffer, cancellationToken);
        try
        {
            await using var scope = await unitOfWork.BeginAsync(cancellationToken);
            await attachments.AddAsync(attachment, cancellationToken);
            audit.Stage(clock ?? TimeProvider.System, request, command.Actor.Id, null, AuditActions.TransferAttachmentAdded, "transfer_slip", slip.Id.ToString("D"), cleanName);
            if (await scope.CommitAsync(cancellationToken) != SaveOutcome.Saved)
            {
                throw new InvalidOperationException("Không lưu được chứng từ đính kèm.");
            }
        }
        catch
        {
            await storage.DeleteAsync(storedName, CancellationToken.None);
            throw;
        }

        return Result<Guid, StockError>.Ok(attachment.Id);
    }
}
