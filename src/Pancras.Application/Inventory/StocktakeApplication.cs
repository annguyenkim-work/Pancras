using Pancras.Domain.Inventory;

namespace Pancras.Application.Inventory;

/// <summary>
/// Áp kết quả kiểm kê đã duyệt vào tồn kho theo chênh lệch (số đếm trừ sổ sách lúc lập), cộng vào tồn hiện tại để giữ các biến động xảy ra giữa lúc lập và lúc duyệt. Không đổi giá vốn.
/// Dùng chung cho duyệt biên bản và biên bản do Kế toán/Giám đốc/Admin lập (duyệt ngay khi tạo).
/// </summary>
internal static class StocktakeApplication
{
    /// <summary>Trả về false chỉ khi tồn sau khi áp sẽ âm. Tồn dưới số đang giữ chỗ vẫn được áp (khả dụng âm).</summary>
    public static async Task<bool> ApplyAsync(
        IInventoryRepository repository,
        StocktakeSheet sheet,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var balance = await repository.FindBalanceForUpdateAsync(sheet.CatalogItemId, sheet.StockLocationId, cancellationToken);
        var created = false;
        if (balance is null)
        {
            balance = StockBalance.Create(sheet.CatalogItemId, sheet.StockLocationId, 0, 0, 0);
            created = true;
        }

        var delta = sheet.Variance;
        if (delta == 0)
        {
            return true;
        }

        if (!balance.TryApplyCount(delta))
        {
            return false;
        }

        await repository.AddMovementAsync(
            StockMovement.Create(sheet.CatalogItemId, sheet.StockLocationId, StockMovementKind.Adjustment, delta, sheet.Id, now),
            cancellationToken);
        if (created)
        {
            await repository.AddBalanceAsync(balance, cancellationToken);
        }

        return true;
    }
}
