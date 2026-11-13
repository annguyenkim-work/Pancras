using Microsoft.Extensions.Logging;
using Pancras.Application.Common;

namespace Pancras.Application.Purchasing;

public sealed class RecordCustomsDeclarationCommandHandler(
    IPurchaseOrderRepository orders,
    ILogger<RecordCustomsDeclarationCommandHandler> logger)
{
    public async Task<Result<Guid>> HandleAsync(RecordCustomsDeclarationCommand command, CancellationToken cancellationToken)
    {
        if ((command.HasCustoms && command.DeclaredCustomsValue < 0)
            || (command.HasDuty && command.DeclaredDuty < 0)
            || (command.HasVat && command.DeclaredVat < 0))
        {
            logger.LogWarning("Declaration rejected for purchase order {PurchaseOrderId}.", command.PurchaseOrderId);
            return Result<Guid>.Fail(PurchaseError.NegativeMoney);
        }

        var order = await orders.FindForUpdateAsync(command.PurchaseOrderId, cancellationToken);
        if (order is null)
        {
            logger.LogWarning("Declaration rejected for purchase order {PurchaseOrderId}.", command.PurchaseOrderId);
            return Result<Guid>.Fail(PurchaseError.InvalidOrder);
        }

        var customs = command.HasCustoms ? command.DeclaredCustomsValue : null;
        var duty = command.HasDuty ? command.DeclaredDuty : null;
        var vat = command.HasVat ? command.DeclaredVat : null;
        if (!order.TryDeclare(command.LineId, customs, duty, vat))
        {
            logger.LogWarning("Declaration rejected for purchase order {PurchaseOrderId}.", order.Id);
            return Result<Guid>.Fail(PurchaseError.NegativeMoney);
        }

        if (!await orders.CommitAsync(cancellationToken))
        {
            logger.LogWarning("Declaration commit failed for purchase order {PurchaseOrderId}.", order.Id);
            return Result<Guid>.Fail(PurchaseError.InvalidOrder);
        }

        logger.LogInformation("Recorded declaration on purchase order {PurchaseOrderId}.", order.Id);
        return Result<Guid>.Ok(command.LineId);
    }
}
