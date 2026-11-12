namespace Pancras.Application.Suppliers;

public static class SupplierTextSearch
{
    public static string ContainsPattern(string value)
    {
        var escaped = value.Trim()
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("%", "\\%", StringComparison.Ordinal)
            .Replace("_", "\\_", StringComparison.Ordinal);
        return "%" + escaped + "%";
    }
}
