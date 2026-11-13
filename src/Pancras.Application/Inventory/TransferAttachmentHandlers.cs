using Microsoft.Extensions.Logging;
using Pancras.Application.Auditing;
using Pancras.Application.Common;
using Pancras.Application.Purchasing;
using Pancras.Domain.Auditing;
using Pancras.Domain.Employees;
using Pancras.Domain.Inventory;
using Pancras.Domain.Purchasing;

namespace Pancras.Application.Inventory;

public interface ITransferAttachmentRepository
{
    Task<IReadOnlyList<TransferAttachment>> ListAsync(Guid transferSlipId, CancellationToken cancellationToken);

    Task<TransferAttachment?> FindAsync(Guid id, CancellationToken cancellationToken);

    Task<IReadOnlyDictionary<Guid, IReadOnlyList<TransferAttachment>>> ListForSlipsAsync(IReadOnlyCollection<Guid> transferSlipIds, CancellationToken cancellationToken);

    Task AddAsync(TransferAttachment attachment, CancellationToken cancellationToken);
}

public sealed record TransferAttachmentView(Guid Id, string FileName, long SizeBytes, DateTimeOffset UploadedAt, string UploadedBy);

/// <summary>Ai được thao tác với một phiếu chuyển: Admin/Giám đốc/Kế toán, hoặc nhân viên kho ở kho nguồn hay kho đích.</summary>
public static class TransferAccess
{
    public static bool CanTouch(EmployeeActor actor, TransferSlip slip) =>
        InventoryAccess.CanOperate(actor.Role)
        && (InventoryAccess.CanAccessLocation(actor, slip.SourceLocationId) || InventoryAccess.CanAccessLocation(actor, slip.DestinationLocationId));

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

public sealed class UploadTransferAttachmentCommandHandler(
    IInventoryRepository inventory,
    ITransferAttachmentRepository attachments,
    IAttachmentStorage storage,
    ILogger<UploadTransferAttachmentCommandHandler> logger,
    TimeProvider? clock = null,
    IAuditLog? audit = null,
    IRequestInfo? request = null)
{
    /// <summary>
    /// Kiểm tra cả loạt file trước khi lưu file nào (quyền, số lượng, đuôi, dung lượng, nội dung khớp đuôi),
    /// để một file sai không để lại các file đứng trước nó.
    /// </summary>
    public async Task<Result<Guid>> CheckAsync(Guid transferSlipId, IReadOnlyList<PendingTransferFile> files, EmployeeActor actor, CancellationToken cancellationToken)
    {
        var slip = await inventory.FindTransferForUpdateAsync(transferSlipId, cancellationToken);
        if (slip is null || !TransferAccess.CanTouch(actor, slip))
        {
            return Result<Guid>.Fail(StockError.Forbidden);
        }

        if (files.Count == 0)
        {
            return Result<Guid>.Ok(slip.Id);
        }

        if ((await attachments.ListAsync(slip.Id, cancellationToken)).Count + files.Count > AttachmentRules.MaxPerOrder)
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

            if (cleanName is null
                || file.Length == 0
                || !AttachmentRules.SignatureMatches(Path.GetExtension(cleanName), file.Header))
            {
                return Result<Guid>.Fail(StockError.AttachmentInvalid);
            }
        }

        return Result<Guid>.Ok(slip.Id);
    }

    public async Task<Result<Guid>> HandleAsync(UploadTransferAttachmentCommand command, CancellationToken cancellationToken)
    {
        var slip = await inventory.FindTransferForUpdateAsync(command.TransferSlipId, cancellationToken);
        if (slip is null || !TransferAccess.CanTouch(command.Actor, slip))
        {
            return Result<Guid>.Fail(StockError.Forbidden);
        }

        var cleanName = AttachmentRules.CleanFileName(command.FileName);
        if (command.Content is null || cleanName is null)
        {
            return Result<Guid>.Fail(StockError.AttachmentInvalid);
        }

        if ((await attachments.ListAsync(slip.Id, cancellationToken)).Count >= AttachmentRules.MaxPerOrder)
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
            logger.LogWarning("Transfer attachment rejected for {TransferSlipId}: empty or content does not match the extension.", slip.Id);
            return Result<Guid>.Fail(StockError.AttachmentInvalid);
        }

        var storedName = Guid.CreateVersion7().ToString("N") + extension.ToLowerInvariant();
        var attachment = TransferAttachment.TryCreate(slip.Id, cleanName, storedName, buffer.Length, (clock ?? TimeProvider.System).GetUtcNow(), request?.ActorCode);
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

        await audit.RecordAsync(clock ?? TimeProvider.System, request, command.Actor.Id, null, AuditActions.TransferAttachmentAdded, "transfer_slip", slip.Id.ToString("D"), cleanName, cancellationToken);
        return Result<Guid>.Ok(attachment.Id);
    }
}

public sealed class OpenTransferAttachmentHandler(
    IInventoryRepository inventory,
    ITransferAttachmentRepository attachments,
    IAttachmentStorage storage,
    TimeProvider? clock = null,
    IAuditLog? audit = null,
    IRequestInfo? request = null)
{
    public async Task<Result<AttachmentDownload>> HandleAsync(Guid attachmentId, EmployeeActor actor, CancellationToken cancellationToken)
    {
        var attachment = await attachments.FindAsync(attachmentId, cancellationToken);
        var slip = attachment is null ? null : await inventory.FindTransferForUpdateAsync(attachment.TransferSlipId, cancellationToken);
        if (attachment is null || slip is null || !TransferAccess.CanTouch(actor, slip))
        {
            return Result<AttachmentDownload>.Fail(StockError.TransferNotFound);
        }

        var content = await storage.OpenReadAsync(attachment.StoredName, cancellationToken);
        if (content is null)
        {
            return Result<AttachmentDownload>.Fail(StockError.TransferNotFound);
        }

        await audit.RecordAsync(clock ?? TimeProvider.System, request, actor.Id, null, AuditActions.TransferAttachmentDownloaded, "transfer_slip", slip.Id.ToString("D"), attachment.FileName, cancellationToken);
        return Result<AttachmentDownload>.Ok(new AttachmentDownload(content, attachment.FileName, attachment.ContentType));
    }
}
