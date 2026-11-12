using System.Globalization;
using System.Text;

namespace Pancras.Domain.Employees;

public readonly record struct EmployeeCodeAttempt(string Code, string NormalizedCode);

public static class EmployeeCodeGenerator
{
    public static string Generate(string fullName, Func<string, bool> normalizedCodeExists)
    {
        foreach (var attempt in Attempts(fullName))
        {
            if (!normalizedCodeExists(attempt.NormalizedCode))
            {
                return attempt.Code;
            }
        }

        throw new InvalidOperationException("No employee code is available.");
    }

    public static bool HasUsableName(string fullName) => CollectParts(fullName).Count > 0;

    public static IEnumerable<EmployeeCodeAttempt> Attempts(string fullName)
    {
        if (!HasUsableName(fullName))
        {
            yield break;
        }

        var baseCode = BuildBase(fullName);
        for (var suffix = 0; suffix < 10000; suffix++)
        {
            var code = suffix == 0
                ? baseCode
                : baseCode + suffix.ToString(CultureInfo.InvariantCulture);
            var normalized = code.ToUpperInvariant();
            if (normalized == "ADMIN")
            {
                continue;
            }

            yield return new EmployeeCodeAttempt(code, normalized);
        }
    }

    private static string BuildBase(string fullName)
    {
        var parts = CollectParts(fullName);
        if (parts.Count == 0)
        {
            throw new ArgumentException("Name has no letters.", nameof(fullName));
        }

        if (parts.Count == 1)
        {
            return parts[0];
        }

        var initials = string.Concat(parts.Take(parts.Count - 1).Select(part => char.ToUpperInvariant(part[0])));
        return parts[^1] + initials;
    }

    private static List<string> CollectParts(string fullName)
    {
        var words = fullName
            .Trim()
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        var parts = new List<string>(words.Length);
        foreach (var word in words)
        {
            var formatted = FormatWord(word);
            if (formatted.Length > 0)
            {
                parts.Add(formatted);
            }
        }

        return parts;
    }

    private static string FormatWord(string word)
    {
        var stripped = StripDiacritics(word);
        var letters = new string(stripped.Where(char.IsLetterOrDigit).ToArray());
        if (letters.Length == 0)
        {
            return string.Empty;
        }

        return char.ToUpperInvariant(letters[0]) + letters[1..].ToLowerInvariant();
    }

    private static string StripDiacritics(string value)
    {
        var replaced = value.Replace('Đ', 'D').Replace('đ', 'd');
        var decomposed = replaced.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        foreach (var character in decomposed)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(character);
            if (category != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(character);
            }
        }

        return builder.ToString().Normalize(NormalizationForm.FormC);
    }
}
