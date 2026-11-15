using Microsoft.AspNetCore.Mvc.ViewFeatures;

namespace Pancras.Web.Formatting;

/// <summary>Mức độ của toast kết quả thao tác (xem docs/03-ui-ux/components.md mục 7).</summary>
public enum ToastLevel
{
    Success,
    Warning,
    Error,
}

/// <summary>Một khóa TempData duy nhất mang cả mức độ lẫn nội dung; _Layout render toast.</summary>
public static class ToastExtensions
{
    public const string Key = "Toast";

    public static void Toast(this ITempDataDictionary tempData, string message, ToastLevel level = ToastLevel.Success)
        => tempData[Key] = $"{level}|{message}";

    public static (ToastLevel Level, string Message)? ReadToast(this ITempDataDictionary tempData)
    {
        if (tempData[Key] is not string raw)
        {
            return null;
        }

        var split = raw.IndexOf('|');
        if (split <= 0 || !Enum.TryParse<ToastLevel>(raw[..split], out var level))
        {
            return (ToastLevel.Success, raw);
        }

        return (level, raw[(split + 1)..]);
    }
}

/// <summary>Dữ liệu cho partial <c>_Notice</c> (banner trong trang hoặc form).</summary>
public sealed record NoticeModel(string Text, ToastLevel Level = ToastLevel.Error);
