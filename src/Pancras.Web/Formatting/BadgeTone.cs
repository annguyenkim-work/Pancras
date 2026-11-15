namespace Pancras.Web.Formatting;

/// <summary>Tông badge (xem docs/03-ui-ux/components.md mục 8). Domain/Application không biết class CSS; chỉ Web ánh xạ.</summary>
public enum BadgeTone
{
    Neutral,
    Info,
    Warning,
    Success,
    Danger,
    Category,
}

public static class Badge
{
    /// <summary>Khung chung của badge.</summary>
    public const string Frame = "rounded-full px-2 py-0.5 text-xs font-bold";

    /// <summary>Màu nền và chữ theo tông (nền -100, chữ -800).</summary>
    public static string Colors(BadgeTone tone) => tone switch
    {
        BadgeTone.Info => "bg-sky-100 text-sky-800",
        BadgeTone.Warning => "bg-amber-100 text-amber-800",
        BadgeTone.Success => "bg-emerald-100 text-emerald-800",
        BadgeTone.Danger => "bg-rose-100 text-rose-800",
        BadgeTone.Category => "bg-violet-100 text-violet-800",
        _ => "bg-slate-100 text-slate-800",
    };

    public static string Classes(BadgeTone tone) => $"{Frame} {Colors(tone)}";
}
