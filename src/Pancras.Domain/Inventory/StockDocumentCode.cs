namespace Pancras.Domain.Inventory;

/// <summary>Mã ngắn hiển thị của chứng từ kho, ví dụ CK-1A2B3C4D cho phiếu chuyển kho.</summary>
public static class StockDocumentCode
{
    public const string Transfer = "CK";

    public const string Stocktake = "KK";

    public const string Adjustment = "DC";

    public const string Disposal = "HY";

    /// <summary>Mã phiếu chuyển kho theo số thứ tự duy nhất, ví dụ CK-000123.</summary>
    public static string ForTransfer(int number) => Transfer + "-" + number.ToString("D6", System.Globalization.CultureInfo.InvariantCulture);

    public static string Of(string prefix, Guid id)
    {
        Span<byte> bytes = stackalloc byte[16];
        id.TryWriteBytes(bytes);
        // Dùng 4 byte cuối (phần ngẫu nhiên của UUID v7). 4 byte đầu là mốc thời gian, nên hai phiếu lập gần nhau sẽ trùng mã.
        return prefix + "-" + Convert.ToHexString(bytes[12..16]);
    }
}
