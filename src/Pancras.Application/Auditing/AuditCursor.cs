using System.Globalization;
using Pancras.Domain.Auditing;

namespace Pancras.Application.Auditing;

/// <summary>
/// Dấu trang của danh sách nhật ký: vị trí (giờ ghi, mã) của dòng ở mép trang. Phân trang keyset không đếm tổng và không nhảy tới trang bất kỳ,
/// nhưng mỗi trang tốn công như nhau dù nhật ký có hàng triệu dòng.
/// </summary>
public sealed record AuditCursor(DateTimeOffset At, Guid Id)
{
    public static AuditCursor Of(AuditEntry entry) => new(entry.OccurredAt, entry.Id);

    public string Encode()
    {
        var raw = At.UtcTicks.ToString(CultureInfo.InvariantCulture) + "." + Id.ToString("N");
        return Convert.ToBase64String(System.Text.Encoding.ASCII.GetBytes(raw)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    /// <summary>Dấu trang hỏng hoặc bị sửa tay thì coi như không có (về trang đầu), không báo lỗi.</summary>
    public static AuditCursor? TryParse(string? token)
    {
        if (string.IsNullOrWhiteSpace(token) || token.Length > 80)
        {
            return null;
        }

        try
        {
            var padded = token.Replace('-', '+').Replace('_', '/');
            padded = padded.PadRight(padded.Length + (4 - padded.Length % 4) % 4, '=');
            var raw = System.Text.Encoding.ASCII.GetString(Convert.FromBase64String(padded));
            var parts = raw.Split('.');
            if (parts.Length == 2
                && long.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var ticks)
                && ticks >= 0 && ticks <= DateTimeOffset.MaxValue.Ticks
                && Guid.TryParseExact(parts[1], "N", out var id))
            {
                return new AuditCursor(new DateTimeOffset(ticks, TimeSpan.Zero), id);
            }
        }
        catch (FormatException)
        {
        }

        return null;
    }
}

/// <summary>Một trang nhật ký theo keyset. <c>Next</c>/<c>Previous</c> là dấu trang để sang trang sau/trước, <c>null</c> nếu hết.</summary>
public sealed record AuditPage(IReadOnlyList<AuditEntry> Entries, string? Next, string? Previous);
