namespace Pancras.Web.Formatting;

/// <summary>Tiêu đề cột bấm được để sắp xếp (partial <c>_SortHeader</c>).</summary>
public sealed record SortHeaderModel(string Label, string Href, bool Active, bool Descending);
