namespace Pancras.Domain.Suppliers;

public static class SupplierEmail
{
    public static bool IsAddress(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var email = value.Trim();
        if (email.Any(char.IsWhiteSpace))
        {
            return false;
        }

        var at = email.IndexOf('@');
        if (at <= 0 || at != email.LastIndexOf('@') || at == email.Length - 1)
        {
            return false;
        }

        var domain = email[(at + 1)..];
        var dot = domain.IndexOf('.');
        return dot > 0 && dot < domain.Length - 1;
    }
}
