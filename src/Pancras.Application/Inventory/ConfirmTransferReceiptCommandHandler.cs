using Pancras.Domain.Auditing;
using Microsoft.Extensions.Logging;
using Pancras.Application.Auditing;
using Pancras.Application.Common;
using Pancras.Application.Employees;
using Pancras.Domain.Inventory;

namespace Pancras.Application.Inventory;

/// <summary>
/// Kiểm nhận ở kho đích: người nhận phải nhập số thực nhận cho từng dòng, kể cả khi nhận đủ.
/// Chỉ cần khác số xuất (thừa hay thiếu đều vậy) là bắt buộc có lý do và ít nhất một file đính kèm;
/// khi đó tồn kho vẫn tăng theo số thực nhận, hệ thống tự lập phiếu điều chỉnh "đã ghi nhận" cho phần chênh
/// (lý do lấy từ lúc nhận) và phiếu chờ Kế toán/Giám đốc xử lý chênh lệch.
/// </summary>
public sealed class ConfirmTransferReceiptCommandHandler(
    IInventoryRepository repository,
    ILogger<ConfirmTransferReceiptCommandHandler> logger,
    TimeProvider? clock = null,
    IAuditLog? audit = null,
    IRequestInfo? request = null,
    ITransferAttachmentRepository? attachments = null,
    IEmployeeRepository? employees = null)
{
    /// <summary>
    /// Kiểm tra trước (không ghi gì) để trang chỉ lưu file đính kèm khi phiếu nhận chắc chắn hợp lệ.
    /// <paramref name="newFileCount"/> là số file người dùng vừa chọn, chưa được lưu.
    /// </summary>
    public async Task<Result<Guid, StockError>> CheckAsync(ConfirmTransferCommand command, int newFileCount, CancellationToken cancellationToken)
    {
        var checkedSlip = await ValidateAsync(command, newFileCount, cancellationToken);
        return checkedSlip.Failure is { } failure ? failure : Result<Guid, StockError>.Ok(command.TransferSlipId);
    }

    public async Task<Result<Guid, StockError>> HandleAsync(ConfirmTransferCommand command, CancellationToken cancellationToken)
    {
        var (slip, failure, actuals) = await ValidateAsync(command, 0, cancellationToken);
        if (failure is not null || slip is null)
        {
            return failure ?? Result<Guid, StockError>.Fail(StockError.ReceiptRejected);
        }

        var destinations = new Dictionary<Guid, StockBalance>();
        foreach (var itemId in slip.Lines.Select(line => line.CatalogItemId).Distinct())
        {
            var destination = await repository.FindBalanceForUpdateAsync(itemId, slip.DestinationLocationId, cancellationToken);
            if (destination is null)
            {
                logger.LogInformation("Receipt rejected for {TransferSlipId}.", slip.Id);
                return Result<Guid, StockError>.Fail(StockError.ReceiptRejected);
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

            logger.LogInformation("Receipt rejected for {TransferSlipId}.", slip.Id);
            return Result<Guid, StockError>.Fail(StockError.ReceiptRejected);
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

        await RecordVarianceAsync(slip, command.Note, cancellationToken);
        await TransferTrail.RecordAsync(
            repository,
            slip,
            slip.HasDiscrepancy ? TransferEventKind.ReceivedWithDiscrepancy : TransferEventKind.Received,
            request,
            clock,
            slip.ReceiveNote,
            cancellationToken);
        await TransferTrail.NotifyAsync(
            repository,
            employees,
            slip,
            command.Actor.Id,
            slip.HasDiscrepancy ? StockNotice.TransferDiscrepancyStep : StockNotice.TransferReceivedStep,
            slip.HasDiscrepancy ? TransferAudience.Creator | TransferAudience.Finance : TransferAudience.Creator,
            clock,
            cancellationToken);
        if (!await repository.CommitAsync(cancellationToken))
        {
            foreach (var snapshot in snapshots)
            {
                snapshot.Restore();
            }

            slip.RestoreUncommitted(status);
            logger.LogWarning("Receipt commit failed for {TransferSlipId}.", slip.Id);
            return Result<Guid, StockError>.Fail(StockError.ReceiptRejected);
        }

        logger.LogInformation("Confirmed receipt {TransferSlipId}.", slip.Id);
        await audit.RecordAsync(clock ?? TimeProvider.System, request, command.Actor.Id, null, AuditActions.TransferReceived, "transfer_slip", slip.Id.ToString("D"), slip.HasDiscrepancy ? "discrepancy" : null, cancellationToken);
        return Result<Guid, StockError>.Ok(slip.Id);
    }

    /// <summary>
    /// Phần thừa/thiếu của từng mặt hàng được ghi thành phiếu điều chỉnh trạng thái "đã ghi nhận" tại kho đích.
    /// Tồn kho đích đã bằng số thực nhận nên phiếu này không cộng trừ tồn lần nữa.
    /// </summary>
    private async Task RecordVarianceAsync(TransferSlip slip, string? reason, CancellationToken cancellationToken)
    {
        var code = StockDocumentCode.Of(StockDocumentCode.Transfer, slip.Id);
        foreach (var group in slip.Lines.GroupBy(line => line.CatalogItemId))
        {
            var delta = group.Sum(line => (line.ReceivedQuantity ?? line.Quantity) - line.Quantity);
            if (delta == 0)
            {
                continue;
            }

            var record = QuantityAdjustment.TryRecordTransferVariance(slip.Id, slip.DestinationLocationId, group.Key, delta, reason, code);
            if (record is not null)
            {
                await repository.AddAdjustmentAsync(record, cancellationToken);
            }
        }
    }

    private async Task<(TransferSlip? Slip, Result<Guid, StockError>? Failure, Dictionary<Guid, int>? Actuals)> ValidateAsync(
        ConfirmTransferCommand command,
        int newFileCount,
        CancellationToken cancellationToken)
    {
        var slip = await repository.FindTransferForUpdateAsync(command.TransferSlipId, cancellationToken);
        if (slip is not null
            && (!InventoryAccess.CanOperate(command.Actor.Role) || !InventoryAccess.CanAccessLocation(command.Actor, slip.DestinationLocationId)))
        {
            logger.LogWarning("Receipt forbidden for employee {EmployeeId}.", command.Actor.Id);
            return (null, Result<Guid, StockError>.Fail(StockError.Forbidden), null);
        }

        if (slip is null)
        {
            return (null, Result<Guid, StockError>.Fail(StockError.TransferNotFound), null);
        }

        if (slip.Status != TransferStatus.InTransit)
        {
            logger.LogInformation("Receipt rejected for {TransferSlipId}.", command.TransferSlipId);
            return (null, Result<Guid, StockError>.Fail(StockError.TransferStateChanged), null);
        }

        if (slip.Lines.Any(line => command.Actuals is null || command.Actuals.All(item => item.LineId != line.Id)))
        {
            return (null, Result<Guid, StockError>.Fail(StockError.QuantityRequired), null);
        }

        var actuals = command.Actuals?.GroupBy(item => item.LineId).ToDictionary(group => group.Key, group => group.Last().Quantity);
        if (actuals is not null && (actuals.Values.Any(value => value < 0) || actuals.Keys.Any(id => slip.Lines.All(line => line.Id != id))))
        {
            return (null, Result<Guid, StockError>.Fail(StockError.ReceiptRejected), null);
        }

        var differs = slip.Lines.Any(line => actuals is not null && actuals.TryGetValue(line.Id, out var value) && value != line.Quantity);
        if (differs)
        {
            if (string.IsNullOrWhiteSpace(command.Note))
            {
                return (null, Result<Guid, StockError>.Fail(StockError.ReasonRequired), null);
            }

            var saved = attachments is null ? 0 : (await attachments.ListAsync(slip.Id, cancellationToken)).Count;
            if (saved + newFileCount == 0)
            {
                return (null, Result<Guid, StockError>.Fail(StockError.AttachmentRequired), null);
            }
        }

        return (slip, null, actuals);
    }
}
