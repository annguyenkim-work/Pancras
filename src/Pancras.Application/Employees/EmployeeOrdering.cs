using Pancras.Application.Common;

namespace Pancras.Application.Employees;

/// <summary>Sắp xếp danh sách nhân viên theo tên, mã, vai trò, kho/showroom; mã là khóa phụ để thứ tự ổn định.</summary>
public static class EmployeeOrdering
{
    public static IReadOnlyList<string> Keys { get; } = ["name", "code", "role", "location"];

    public static ListSort Parse(string? token) => ListSort.Parse(token, Keys, "name");

    public static IReadOnlyList<EmployeeCard> Apply(IEnumerable<EmployeeCard> cards, ListSort sort)
    {
        var comparer = StringComparer.OrdinalIgnoreCase;
        IOrderedEnumerable<EmployeeCard> ordered = (sort.Key, sort.Descending) switch
        {
            ("code", false) => cards.OrderBy(card => card.Code, comparer),
            ("code", true) => cards.OrderByDescending(card => card.Code, comparer),
            ("role", false) => cards.OrderBy(card => card.Role, comparer).ThenBy(card => card.Code, comparer),
            ("role", true) => cards.OrderByDescending(card => card.Role, comparer).ThenBy(card => card.Code, comparer),
            ("location", false) => cards.OrderBy(card => card.LocationName, comparer).ThenBy(card => card.Code, comparer),
            ("location", true) => cards.OrderByDescending(card => card.LocationName, comparer).ThenBy(card => card.Code, comparer),
            (_, true) => cards.OrderByDescending(card => card.FullName, comparer).ThenBy(card => card.Code, comparer),
            _ => cards.OrderBy(card => card.FullName, comparer).ThenBy(card => card.Code, comparer)
        };
        return ordered.ToArray();
    }
}
