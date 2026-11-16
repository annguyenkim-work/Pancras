namespace Pancras.Application.Common;

/// <summary>Thứ tự sắp xếp của danh sách: khóa cột và chiều. Dạng chuỗi trên URL: <c>name</c> (tăng), <c>-name</c> (giảm).</summary>
public sealed record ListSort(string Key, bool Descending)
{
    public string Token => Descending ? "-" + Key : Key;

    /// <summary>Chỉ nhận khóa nằm trong danh sách cho phép; khác thì dùng khóa mặc định (tăng dần).</summary>
    public static ListSort Parse(string? token, IReadOnlyList<string> allowed, string defaultKey)
    {
        var text = token?.Trim() ?? string.Empty;
        var descending = text.StartsWith('-');
        var key = (descending ? text[1..] : text).ToLowerInvariant();
        var match = allowed.FirstOrDefault(candidate => candidate == key);
        return match is null ? new ListSort(defaultKey, false) : new ListSort(match, descending);
    }

    /// <summary>Bấm lại đúng cột đang sắp thì đảo chiều; bấm cột khác thì sắp tăng dần theo cột đó.</summary>
    public ListSort Toggle(string key) => key == Key ? this with { Descending = !Descending } : new ListSort(key, false);
}
