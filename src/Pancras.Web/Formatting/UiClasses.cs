namespace Pancras.Web.Formatting;

/// <summary>Nguồn class chung cho form (docs/03-ui-ux/components.md mục 2). Trang không tự viết lại.</summary>
public static class UiClasses
{
    public const string Label = "block text-sm font-medium text-slate-700";

    public const string Input = "mt-1 w-full rounded-lg border border-slate-300 px-3 py-2 text-sm focus:border-sky-600 focus:outline-none";

    /// <summary>Ô có lỗi: viền đỏ; đi kèm <c>aria-invalid="true"</c> và câu lỗi ngay dưới ô.</summary>
    public const string InputInvalid = "mt-1 w-full rounded-lg border border-rose-500 px-3 py-2 text-sm focus:border-rose-500 focus:outline-none";
}
