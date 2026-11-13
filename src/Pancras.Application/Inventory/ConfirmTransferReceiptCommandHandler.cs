using Pancras.Domain.Auditing;
using Microsoft.Extensions.Logging;
using Pancras.Application.Auditing;
using Pancras.Application.Common;
using Pancras.Domain.Inventory;

namespace Pancras.Application.Inventory;

/// <summary>
/// Kiểm nhận ở kho đích: người nhận nhập số thực nhận từng dòng (bỏ trống = nhận đủ).
/// Chỉ cần khác số xuất (thừa hay thiếu đều vậy) là bắt buộc có lý do và ít nhất một file đính kèm;
/// khi đó tồn kho vẫn tăng theo số thực nhận và phiếu chờ Kế toán/Giám đốc xử lý chênh lệch.
/// </summary>
public sealed class ConfirmTransferReceiptCommandHandler(
    IInventoryRepository repository,
    ILogger<ConfirmTransferReceiptCommandHandler> logger,
    TimeProvider? clock = null,
    IAuditLog? audit = null,
    IRequestInfo? request = null,
    ITransferAttachmentRepository? attachments = null)
{
    /// <summary>
    /// Kiểm tra trước (không ghi gì) để trang chỉ lưu file đính kèm khi phiếu nhận chắc chắn hợp lệ.
    /// <paramref name="newFileCount"/> là số file người dùng vừa chọn, chưa được lưu.
    /// </summary>
    public async Task<Result<Guid>> CheckAsync(ConfirmTransferCommand command, int newFileCount, CancellationToken cancellationToken)
    {
        var checkedSlip = await ValidateAsync(command, newFileCount, cancellationToken);
        return checkedSlip.Failure is { } failure ? failure : Result<Guid>.Ok(command.TransferSlipId);
    }

    public async Task<Result<Guid>> HandleAsync(ConfirmTransferCommand command, CancellationToken cancellationToken)
    {
        var (slip, failure, actuals) = await ValidateAsync(command, 0, cancellationToken);
        if (failure is not null || slip is null)
        {
            return failure ?? Result<Guid>.Fail(StockError.ReceiptRejected);
        }

        var destinations = new Dictionary<Guid, StockBalance>();
        foreach (var itemId in slip.Lines.Select(line => line.CatalogItemId).Distinct())
        {
            var destination = await repository.FindBalanceForUpdateAsync(itemId, slip.DestinationLocationId, cancellationToken);
            if (destination is null)
            {
                logger.LogWarning("Receipt rejected for {TransferSlipId}.", slip.Id);
                return Result<Guid>.Fail(StockError.ReceiptRejected);
            }

            destinations[itemId] = destination;
        }

        var snapshots = destinations.Values.Select(BalanceSnapshot.Take).ToArray();
        var status = slip.Status;
        if (!slip.TryConfirmReceipt(destinations, actuals, command.Note))
        {
            foreach (var snapshot in snapshots)
            {
                snapshot.Restore();
            }

            logger.LogWarning("Receipt rejected for {TransferSlipId}.", slip.Id);
            return Result<Guid>.Fail(StockError.ReceiptRejected);
        }

        var occurredAt = (clock ?? TimeProvider.System).GetUtcNow();
        slip.RecordStep(TransferStep.Received, occurredAt, request?.ActorCode);
        foreach (var line in slip.Lines)
        {
            var received = line.ReceivedQuantity ?? line.Quantity;
            if (received == 0)
            {
                // Thực nhận 0: không có biến động tồn ở kho đích (chênh lệch đã nằm trên phiếu).
                continue;
            }

            await repository.AddMovementAsync(
                StockMovement.Create(line.CatalogItemId, slip.DestinationLocationId, StockMovementKind.Transfer, received, slip.Id, occurredAt),
                cancellationToken);
        }

        if (!await repository.CommitAsync(cancellationToken))
        {
            foreach (var snapshot in snapshots)
            {
                snapshot.Restore();
            }

            slip.RestoreUncommitted(status);
            logger.LogWarning("Receipt commit failed for {TransferSlipId}.", slip.Id);
            return Result<Guid>.Fail(StockError.ReceiptRejected);
        }

        logger.LogInformation("Confirmed receipt {TransferSlipId}.", slip.Id);
        await audit.RecordAsync(clock ?? TimeProvider.System, request, command.Actor.Id, null, AuditActions.TransferReceived, "transfer_slip", slip.Id.ToString("D"), slip.HasDiscrepancy ? "discrepancy" : null, cancellationToken);
        return Result<Guid>.Ok(slip.Id);
    }

    private async Task<(TransferSlip? Slip, Result<Guid>? Failure, Dictionary<Guid, int>? Actuals)> ValidateAsync(
        ConfirmTransferCommand command,
        int newFileCount,
        CancellationToken cancellationToken)
    {
        var slip = await repository.FindTransferForUpdateAsync(command.TransferSlipId, cancellationToken);
        if (slip is not null
            && (!InventoryAccess.CanOperate(command.Actor.Role) || !InventoryAccess.CanAccessLocation(command.Actor, slip.DestinationLocationId)))
        {
            logger.LogWarning("Receipt forbidden for employee {EmployeeId}.", command.Actor.Id);
            return (null, Result<Guid>.Fail(StockError.Forbidden), null);
        }

        if (slip is null || slip.Status != TransferStatus.InTransit)
        {
            logger.LogWarning("Receipt rejected for {TransferSlipId}.", command.TransferSlipId);
            return (null, Result<Guid>.Fail(StockError.ReceiptRejected), null);
        }

        var actuals = command.Actuals?.GroupBy(item => item.LineId).ToDictionary(group => group.Key, group => group.Last().Quantity);
        if (actuals is not null && (actuals.Values.Any(value => value < 0) || actuals.Keys.Any(id => slip.Lines.All(line => line.Id != id))))
        {
            return (null, Result<Guid>.Fail(StockError.ReceiptRejected), null);
        }

        var differs = slip.Lines.Any(line => actuals is not null && actuals.TryGetValue(line.Id, out var value) && value != line.Quantity);
        if (differs)
        {
            if (string.IsNullOrWhiteSpace(command.Note))
            {
                return (null, Result<Guid>.Fail(StockError.ReasonRequired), null);
            }

            var saved = attachments is null ? 0 : (await attachments.ListAsync(slip.Id, cancellationToken)).Count;
            if (saved + newFileCount == 0)
            {
                return (null, Result<Guid>.Fail(StockError.AttachmentRequired), null);
            }
        }

        return (slip, null, actuals);
    }
}
