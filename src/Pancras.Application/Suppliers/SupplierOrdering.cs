using Pancras.Application.Common;
using Pancras.Domain.Suppliers;

namespace Pancras.Application.Suppliers;

/// <summary>Sắp xếp danh sách nhà cung cấp theo cột tên, mã, quốc gia; mã là khóa phụ để thứ tự ổn định.</summary>
public static class SupplierOrdering
{
    public static IReadOnlyList<string> Keys { get; } = ["name", "code", "country"];

    public static ListSort Parse(string? token) => ListSort.Parse(token, Keys, "name");

    public static IQueryable<Supplier> Apply(IQueryable<Supplier> suppliers, ListSort sort) => (sort.Key, sort.Descending) switch
    {
        ("code", false) => suppliers.OrderBy(supplier => supplier.Code),
        ("code", true) => suppliers.OrderByDescending(supplier => supplier.Code),
        ("country", false) => suppliers.OrderBy(supplier => supplier.Country).ThenBy(supplier => supplier.Code),
        ("country", true) => suppliers.OrderByDescending(supplier => supplier.Country).ThenBy(supplier => supplier.Code),
        (_, true) => suppliers.OrderByDescending(supplier => supplier.Name).ThenBy(supplier => supplier.Code),
        _ => suppliers.OrderBy(supplier => supplier.Name).ThenBy(supplier => supplier.Code)
    };
}
