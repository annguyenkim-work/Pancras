namespace Pancras.Web.Formatting;

/// <summary>Một tab của partial <c>_Tabs</c>. <c>Badge</c> là số nhỏ cạnh nhãn (ví dụ phiếu chờ xử lý), null thì không hiện.</summary>
public sealed record TabItem(string Id, string Label, string Href, bool Active, int? Badge = null, string? BadgeTitle = null);

public sealed record TabsModel(string Label, IReadOnlyList<TabItem> Items);
