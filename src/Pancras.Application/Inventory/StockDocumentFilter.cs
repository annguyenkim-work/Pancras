using System.Text.RegularExpressions;
using Pancras.Domain.Inventory;

namespace Pancras.Application.Inventory;

/// <summary>
/// Bộ lọc của tab Kiểm kê và điều chỉnh: loại phiếu và mã phiếu (mã hiển thị <c>KK-1A2B3C4D</c>, xem <see cref="StockDocumentCode"/>).
/// Mã không phân biệt hoa thường: nhập đoạn hex (<c>1A2B</c>) thì khớp phiếu có đoạn đó trong 8 ký tự mã; nhập tiền tố kèm gạch ngang
/// (<c>KK-</c>) thì chỉ tìm trong loại đó. Mã không thể khớp phiếu nào thì không loại nào được chọn.
/// </summary>
public sealed partial record StockDocumentFilter(StockDocumentKind? Kind = null, string? Code = null)
{
    private const int CodeLength = 8;

    public bool IsActive => Kind is not null || !string.IsNullOrWhiteSpace(Code);

    /// <summary>Đoạn hex (chữ hoa) cần có trong mã phiếu; null khi không lọc theo mã.</summary>
    public string? CodeFragment => Resolve().Fragment;

    public bool Includes(StockDocumentKind kind)
    {
        var (prefixKind, _, impossible) = Resolve();
        return !impossible
            && (Kind is null || Kind == kind)
            && (prefixKind is null || prefixKind == kind);
    }

    /// <summary>Đọc mã loại phiếu trên địa chỉ: <c>kk</c>, <c>dc</c>, <c>hy</c>; giá trị khác là tất cả (null).</summary>
    public static StockDocumentKind? ParseKind(string? token) => token?.Trim().ToUpperInvariant() switch
    {
        StockDocumentCode.Stocktake => StockDocumentKind.Stocktake,
        StockDocumentCode.Adjustment => StockDocumentKind.Adjustment,
        StockDocumentCode.Disposal => StockDocumentKind.Disposal,
        _ => null
    };

    /// <summary>Ngược lại của <see cref="ParseKind"/>, để dựng địa chỉ.</summary>
    public static string? KindToken(StockDocumentKind? kind) => kind switch
    {
        StockDocumentKind.Stocktake => "kk",
        StockDocumentKind.Adjustment => "dc",
        StockDocumentKind.Disposal => "hy",
        _ => null
    };

    private (StockDocumentKind? PrefixKind, string? Fragment, bool Impossible) Resolve()
    {
        var typed = Code?.Trim().ToUpperInvariant();
        if (string.IsNullOrEmpty(typed))
        {
            return (null, null, false);
        }

        StockDocumentKind? prefixKind = null;
        var rest = typed;
        var prefixed = PrefixPattern().Match(typed);
        if (prefixed.Success)
        {
            prefixKind = ParseKind(prefixed.Groups[1].Value);
            if (prefixKind is null)
            {
                return (null, null, true);
            }

            rest = prefixed.Groups[2].Value.Trim();
        }

        if (rest.Length == 0)
        {
            return (prefixKind, null, false);
        }

        return rest.Length > CodeLength || !HexPattern().IsMatch(rest)
            ? (prefixKind, null, true)
            : (prefixKind, rest, false);
    }

    [GeneratedRegex("^([A-Z]{2})-(.*)$")]
    private static partial Regex PrefixPattern();

    [GeneratedRegex("^[0-9A-F]+$")]
    private static partial Regex HexPattern();
}
