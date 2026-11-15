namespace Pancras.Domain.Inventory;

public sealed class StockBalance
{
    private StockBalance()
    {
    }

    public Guid Id { get; private set; }

    public Guid CatalogItemId { get; private set; }

    public Guid StockLocationId { get; private set; }

    public int OnHand { get; private set; }

    public int Reserved { get; private set; }

    public int InTransit { get; private set; }

    public int Version { get; private set; }

    public int Available => OnHand - Reserved;

    public static StockBalance Create(
        Guid catalogItemId,
        Guid stockLocationId,
        int onHand,
        int reserved,
        int inTransit)
    {
        if (catalogItemId == Guid.Empty)
        {
            throw new ArgumentException("Catalog item is required.", nameof(catalogItemId));
        }

        if (stockLocationId == Guid.Empty)
        {
            throw new ArgumentException("Stock location is required.", nameof(stockLocationId));
        }

        if (onHand < 0 || reserved < 0 || inTransit < 0 || onHand < reserved)
        {
            throw new ArgumentOutOfRangeException(nameof(onHand));
        }

        return new StockBalance
        {
            Id = Guid.CreateVersion7(),
            CatalogItemId = catalogItemId,
            StockLocationId = stockLocationId,
            OnHand = onHand,
            Reserved = reserved,
            InTransit = inTransit
        };
    }

    public bool CanShip(int quantity) => quantity > 0 && Available >= quantity;

    public bool TryShip(int quantity)
    {
        if (!CanShip(quantity))
        {
            return false;
        }

        OnHand -= quantity;
        Version++;
        return true;
    }

    /// <summary>Giữ hàng cho phiếu chuyển đã duyệt: tăng số giữ chỗ, chưa trừ tồn.</summary>
    public bool TryReserve(int quantity)
    {
        if (!CanShip(quantity))
        {
            return false;
        }

        Reserved += quantity;
        Version++;
        return true;
    }

    /// <summary>Nhả phần đã giữ (hủy phiếu đã duyệt).</summary>
    public bool TryRelease(int quantity)
    {
        if (quantity <= 0 || Reserved < quantity)
        {
            return false;
        }

        Reserved -= quantity;
        Version++;
        return true;
    }

    /// <summary>Xuất phần đã được giữ: trừ cả tồn lẫn số giữ chỗ.</summary>
    public bool TryShipReserved(int quantity)
    {
        if (quantity <= 0 || Reserved < quantity || OnHand < quantity)
        {
            return false;
        }

        OnHand -= quantity;
        Reserved -= quantity;
        Version++;
        return true;
    }

    public bool TryAddInTransit(int quantity)
    {
        if (quantity <= 0 || (long)InTransit + quantity > int.MaxValue)
        {
            return false;
        }

        InTransit += quantity;
        Version++;
        return true;
    }

    public bool TryRemoveInTransit(int quantity)
    {
        if (quantity <= 0 || InTransit < quantity)
        {
            return false;
        }

        InTransit -= quantity;
        Version++;
        return true;
    }

    public bool TryReceiveTransfer(int quantity)
    {
        if (quantity <= 0 || InTransit < quantity || (long)OnHand + quantity > int.MaxValue)
        {
            return false;
        }

        InTransit -= quantity;
        OnHand += quantity;
        Version++;
        return true;
    }

    /// <summary>Nhận hàng chuyển: bỏ phần đang chuyển đã xuất, cộng số thực nhận (có thể khác số xuất).</summary>
    public bool TryReceiveActual(int shipped, int actual)
    {
        if (shipped <= 0 || actual < 0 || InTransit < shipped || (long)OnHand + actual > int.MaxValue)
        {
            return false;
        }

        InTransit -= shipped;
        OnHand += actual;
        Version++;
        return true;
    }

    public bool TryAddOnHand(int quantity)
    {
        if (quantity <= 0 || (long)OnHand + quantity > int.MaxValue)
        {
            return false;
        }

        OnHand += quantity;
        Version++;
        return true;
    }

    public bool TryAdjust(int delta)
    {
        if (delta == 0)
        {
            return false;
        }

        var next = (long)OnHand + delta;
        if (next < Reserved || next > int.MaxValue)
        {
            return false;
        }

        OnHand = (int)next;
        Version++;
        return true;
    }

    /// <summary>Áp chênh lệch kiểm kê. Khác điều chỉnh: cho phép tồn xuống dưới số đang giữ chỗ (khả dụng âm), nhưng không để tồn âm.</summary>
    public bool TryApplyCount(int delta)
    {
        var next = (long)OnHand + delta;
        if (delta == 0 || next < 0 || next > int.MaxValue)
        {
            return false;
        }

        OnHand = (int)next;
        Version++;
        return true;
    }

    public bool TryDisposeOne()
    {
        if (Available < 1)
        {
            return false;
        }

        OnHand -= 1;
        Version++;
        return true;
    }

    public void RestoreUncommitted(int onHand, int reserved, int inTransit)
    {
        OnHand = onHand;
        Reserved = reserved;
        InTransit = inTransit;
    }
}
