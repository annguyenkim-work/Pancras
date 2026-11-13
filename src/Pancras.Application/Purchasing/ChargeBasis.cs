using Pancras.Application.Catalog;
using Pancras.Domain.Purchasing;

namespace Pancras.Application.Purchasing;

/// <summary>Cơ sở chia một khoản chi phí cho các dòng hàng. Trả về null khi thiếu số đo (cân nặng, thể tích).</summary>
internal static class ChargeBasis
{
    /// <summary>
    /// Thêm một khoản chi phí chung, chia theo <paramref name="method"/> (đúng cách người dùng chọn, không tự đổi) và gán nhóm.
    /// Trả về false nếu không chia được (thiếu thể tích/cân nặng, nhóm hoặc cách chia không hợp lệ). Khi đó khoản không còn trong đơn.
    /// </summary>
    public static async Task<bool> TryAddSplitAsync(
        PurchaseOrder order,
        decimal amount,
        string? label,
        ChargeGroup group,
        ChargeSplitMethod method,
        GetCatalogMeasuresHandler? measures,
        CancellationToken cancellationToken)
    {
        if (method == ChargeSplitMethod.Manual || !Enum.IsDefined(method) || !Enum.IsDefined(group))
        {
            return false;
        }

        if (!order.TryAddCharge(amount, method, out var chargeId, label))
        {
            return false;
        }

        var basis = await ComputeAsync(order, method, null, measures, cancellationToken);
        if (basis is not null && order.TryAllocate(chargeId, basis, manual: false) && order.TryAssignGroup(chargeId, group, retainCurrent: false))
        {
            return true;
        }

        order.DropCharge(chargeId);
        return false;
    }

    public static async Task<IReadOnlyDictionary<Guid, decimal>?> ComputeAsync(
        PurchaseOrder order,
        ChargeSplitMethod method,
        IReadOnlyDictionary<Guid, decimal>? manualShares,
        GetCatalogMeasuresHandler? measures,
        CancellationToken cancellationToken)
    {
        if (method == ChargeSplitMethod.Manual)
        {
            return manualShares ?? new Dictionary<Guid, decimal>();
        }

        if (method is ChargeSplitMethod.Weight or ChargeSplitMethod.Volume)
        {
            if (measures is null)
            {
                return null;
            }

            var ids = order.Lines.Select(line => line.CatalogItemId).Distinct().ToArray();
            var catalog = await measures.HandleAsync(ids, cancellationToken);
            var basis = new Dictionary<Guid, decimal>();
            foreach (var line in order.Lines)
            {
                if (!catalog.TryGetValue(line.CatalogItemId, out var measure))
                {
                    return null;
                }

                var unit = method == ChargeSplitMethod.Weight ? measure.WeightKg : measure.VolumeM3;
                if (unit is null)
                {
                    return null;
                }

                basis[line.Id] = unit.Value * line.Quantity;
            }

            return basis;
        }

        return order.Lines.ToDictionary(
            line => line.Id,
            line => method == ChargeSplitMethod.Quantity ? (decimal)line.Quantity : line.FobUsd);
    }
}
