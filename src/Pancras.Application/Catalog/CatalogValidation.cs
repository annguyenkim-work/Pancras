using FluentValidation.Results;
using Pancras.Application.Common;
using Pancras.Domain.Catalog;

namespace Pancras.Application.Catalog;

internal static class CatalogValidation
{
    public static IReadOnlyList<CatalogIssue> IssuesFrom(ValidationResult validation) =>
        validation.Errors
            .Select(error => error.CustomState)
            .OfType<CatalogIssue>()
            .Distinct()
            .ToArray();

    public static CatalogError Primary(IReadOnlyList<CatalogIssue> issues)
    {
        if (issues.Any(issue => issue.Kind == CatalogIssueKind.Missing))
        {
            return CatalogError.Incomplete;
        }

        if (issues.Any(issue => issue.Kind == CatalogIssueKind.TooLong))
        {
            return CatalogError.TooLong;
        }

        if (issues.Any(issue => issue.Kind == CatalogIssueKind.Negative))
        {
            return CatalogError.Negative;
        }

        if (issues.Any(issue => issue.Kind == CatalogIssueKind.TooLarge))
        {
            return CatalogError.TooLarge;
        }

        return CatalogError.Incomplete;
    }

    public static bool HasText(string? value) => !string.IsNullOrWhiteSpace(value);

    public static bool Fits(string? value, int maxLength) =>
        string.IsNullOrWhiteSpace(value) || value.Trim().Length <= maxLength;

    public static bool FitsCode(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        var trimmed = value.Trim();
        return trimmed.Length <= CatalogItem.CodeMaxLength
            && CatalogItem.NormalizeCode(trimmed).Length <= CatalogItem.CodeMaxLength;
    }

    public static bool KnownCategory(ProductCategory? category) =>
        category is null || Enum.IsDefined(category.Value);

    public static bool NotNegative(decimal? value) => value is null || value >= 0;

    public static bool NotNegative(int? value) => value is null || value >= 0;

    public static bool PriceFits(decimal? value) =>
        value is null || decimal.Round(value.Value, 0, MidpointRounding.AwayFromZero) <= CatalogItem.MaxPrice;

    public static bool MeasureFits(decimal? value) =>
        value is null || decimal.Round(value.Value, 3, MidpointRounding.AwayFromZero) <= CatalogItem.MaxMeasure;
}
