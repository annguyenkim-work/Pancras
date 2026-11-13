namespace Pancras.Domain.Suppliers;

/// <summary>Mã SWIFT/BIC: 8 hoặc 11 ký tự chữ và số (chuẩn ISO 9362).</summary>
public static class SupplierSwift
{
    public static bool IsCode(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var code = value.Trim();
        return code.Length is 8 or 11 && code.All(char.IsAsciiLetterOrDigit);
    }
}
