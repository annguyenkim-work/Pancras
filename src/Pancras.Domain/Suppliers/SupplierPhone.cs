namespace Pancras.Domain.Suppliers;

public static class SupplierPhone
{
    public const int MinDigits = 8;

    public const int MaxDigits = 15;

    public static bool IsNumber(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var phone = value.Trim();
        var digits = 0;
        for (var index = 0; index < phone.Length; index++)
        {
            var character = phone[index];
            if (char.IsAsciiDigit(character))
            {
                digits++;
                continue;
            }

            if (character is ' ' or '-' or '(' or ')' or '.')
            {
                continue;
            }

            if (character == '+' && index == 0)
            {
                continue;
            }

            return false;
        }

        return digits is >= MinDigits and <= MaxDigits;
    }
}
