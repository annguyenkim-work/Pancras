using Pancras.Application.Common;
using Pancras.Domain.Catalog;

namespace Pancras.Application.Catalog;

/// <summary>Sắp xếp danh mục theo cột tên, mã, thương hiệu; mã là khóa phụ để thứ tự ổn định.</summary>
public static class CatalogOrdering
{
    public static IReadOnlyList<string> Keys { get; } = ["name", "code", "brand"];

    public static ListSort Parse(string? token) => ListSort.Parse(token, Keys, "name");

    public static IQueryable<CatalogItem> Apply(IQueryable<CatalogItem> items, ListSort sort) => (sort.Key, sort.Descending) switch
    {
        ("code", false) => items.OrderBy(item => item.Code),
        ("code", true) => items.OrderByDescending(item => item.Code),
        ("brand", false) => items.OrderBy(item => item.Brand).ThenBy(item => item.Code),
        ("brand", true) => items.OrderByDescending(item => item.Brand).ThenBy(item => item.Code),
        (_, true) => items.OrderByDescending(item => item.Name).ThenBy(item => item.Code),
        _ => items.OrderBy(item => item.Name).ThenBy(item => item.Code)
    };
}
