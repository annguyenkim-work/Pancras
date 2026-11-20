using Microsoft.Extensions.Logging;
using Pancras.Domain.Employees;
using Pancras.Domain.Purchasing;

namespace Pancras.Application.Purchasing;

public sealed record ShipmentDuplicate(Guid Id, string Code, string Shared);

/// <summary>
/// Cảnh báo (không chặn): đơn khác còn hiệu lực cùng số container hoặc cùng vận đơn (B/L). So khớp không phân biệt hoa thường và bỏ khoảng trắng.
/// </summary>
public sealed class FindShipmentDuplicatesHandler(
    IPurchaseOrderRepository orders,
    ILogger<FindShipmentDuplicatesHandler> logger)
{
    public async Task<IReadOnlyList<ShipmentDuplicate>> HandleAsync(Guid orderId, EmployeeActor actor, CancellationToken cancellationToken)
    {
        if (!PurchaseAccess.CanManage(actor))
        {
            return [];
        }

        var all = await orders.ListShipmentKeysAsync(cancellationToken);
        var mine = all.FirstOrDefault(order => order.Id == orderId);
        if (mine is null)
        {
            return [];
        }

        var container = Normalize(mine.ContainerNumber);
        var billOfLading = Normalize(mine.BillOfLading);
        var found = new List<ShipmentDuplicate>();
        foreach (var other in all.Where(order => order.Id != orderId && order.Status != PurchaseStatus.Cancelled))
        {
            var sameContainer = container.Length > 0 && Normalize(other.ContainerNumber) == container;
            var sameBill = billOfLading.Length > 0 && Normalize(other.BillOfLading) == billOfLading;
            if (sameContainer || sameBill)
            {
                found.Add(new ShipmentDuplicate(other.Id, other.Code, sameContainer && sameBill ? "container và B/L" : sameContainer ? "container" : "B/L"));
            }
        }

        logger.LogInformation("Found {DuplicateCount} orders sharing a shipment number with {PurchaseOrderId}.", found.Count, orderId);
        return found;
    }

    private static string Normalize(string? value) =>
        new string((value ?? string.Empty).Where(character => !char.IsWhiteSpace(character)).ToArray()).ToUpperInvariant();
}
