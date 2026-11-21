using System.Globalization;
using Pancras.Application.Catalog;
using Pancras.Application.Common;

namespace Pancras.Web.Formatting;

public static class CatalogFormReader
{
    public static bool TryReadCreate(
        CatalogItemForm form,
        out CreateCatalogItemCommand command,
        out IReadOnlyList<CatalogIssue> issues)
    {
        var found = new List<CatalogIssue>();
        command = new CreateCatalogItemCommand
        {
            Code = form.Code,
            Name = form.Name,
            Brand = form.Brand,
            Origin = form.Origin,
            Unit = form.Unit
        };

        if (string.IsNullOrWhiteSpace(form.Category))
        {
            found.Add(new CatalogIssue(CatalogField.Category, CatalogIssueKind.Missing));
        }
        else if (ProductCategoryToken.TryParse(form.Category, out var category))
        {
            command.Category = category;
        }
        else
        {
            found.Add(new CatalogIssue(CatalogField.Category, CatalogIssueKind.Missing));
        }

        command.WeightKg = Optional(form.WeightKg, CatalogField.Weight, false, found);
        command.VolumeM3 = Optional(form.VolumeM3, CatalogField.Volume, false, found);
        command.MinimumStock = Optional(form.MinimumStock, CatalogField.MinimumStock, false, found);
        command.HeightMm = OptionalMillimeter(form.HeightMm, CatalogField.Height, found);
        command.LengthMm = OptionalMillimeter(form.LengthMm, CatalogField.Length, found);
        command.WidthMm = OptionalMillimeter(form.WidthMm, CatalogField.Width, found);
        command.WarrantyMonths = OptionalInt(form.WarrantyMonths, found);
        command.CostPrice = Optional(form.CostPrice, CatalogField.CostPrice, true, found);
        command.RetailPrice = Optional(form.RetailPrice, CatalogField.RetailPrice, true, found);
        command.DealerPrice = Optional(form.DealerPrice, CatalogField.DealerPrice, true, found);
        command.DistributorPrice = Optional(form.DistributorPrice, CatalogField.DistributorPrice, true, found);
        command.ProjectPrice = Optional(form.ProjectPrice, CatalogField.ProjectPrice, true, found);
        command.DealerDiscountPercent = Optional(form.DealerDiscountPercent, CatalogField.DealerDiscountPercent, false, found);
        command.DistributorDiscountPercent = Optional(form.DistributorDiscountPercent, CatalogField.DistributorDiscountPercent, false, found);
        command.ProjectDiscountPercent = Optional(form.ProjectDiscountPercent, CatalogField.ProjectDiscountPercent, false, found);
        issues = found;
        return found.Count == 0;
    }

    public static bool TryReadPrices(
        CatalogPriceForm form,
        string? role,
        out UpdateCatalogPricesCommand command,
        out IReadOnlyList<CatalogIssue> issues)
    {
        var found = new List<CatalogIssue>();
        command = new UpdateCatalogPricesCommand
        {
            Id = form.Id,
            Role = role,
            IncludeDetails = form.IncludeDetails,
            Code = form.Code,
            Name = form.Name,
            Brand = form.Brand,
            Origin = form.Origin,
            Unit = form.Unit,
            CostPrice = Optional(form.CostPrice, CatalogField.CostPrice, true, found),
            RetailPrice = Optional(form.RetailPrice, CatalogField.RetailPrice, true, found),
            DealerPrice = Optional(form.DealerPrice, CatalogField.DealerPrice, true, found),
            DistributorPrice = Optional(form.DistributorPrice, CatalogField.DistributorPrice, true, found),
            ProjectPrice = Optional(form.ProjectPrice, CatalogField.ProjectPrice, true, found),
            DealerDiscountPercent = Optional(form.DealerDiscountPercent, CatalogField.DealerDiscountPercent, false, found),
            DistributorDiscountPercent = Optional(form.DistributorDiscountPercent, CatalogField.DistributorDiscountPercent, false, found),
            ProjectDiscountPercent = Optional(form.ProjectDiscountPercent, CatalogField.ProjectDiscountPercent, false, found),
            Version = form.Version
        };
        if (form.IncludeDetails)
        {
            if (string.IsNullOrWhiteSpace(form.Category))
            {
                found.Add(new CatalogIssue(CatalogField.Category, CatalogIssueKind.Missing));
            }
            else if (ProductCategoryToken.TryParse(form.Category, out var category))
            {
                command.Category = category;
            }
            else
            {
                found.Add(new CatalogIssue(CatalogField.Category, CatalogIssueKind.Missing));
            }

            command.WeightKg = Optional(form.WeightKg, CatalogField.Weight, false, found);
            command.VolumeM3 = Optional(form.VolumeM3, CatalogField.Volume, false, found);
            command.MinimumStock = Optional(form.MinimumStock, CatalogField.MinimumStock, false, found);
            command.HeightMm = OptionalMillimeter(form.HeightMm, CatalogField.Height, found);
            command.LengthMm = OptionalMillimeter(form.LengthMm, CatalogField.Length, found);
            command.WidthMm = OptionalMillimeter(form.WidthMm, CatalogField.Width, found);
            command.WarrantyMonths = OptionalInt(form.WarrantyMonths, found);
        }

        issues = found;
        return found.Count == 0;
    }

    private static decimal? Optional(string? text, CatalogField field, bool money, List<CatalogIssue> issues)
    {
        if (TryParse(text, money, out var amount))
        {
            return amount;
        }

        issues.Add(new CatalogIssue(field, CatalogIssueKind.InvalidNumber));
        return null;
    }

    private static int? OptionalMillimeter(string? text, CatalogField field, List<CatalogIssue> issues)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var cleaned = text.Trim().Replace(" ", "", StringComparison.Ordinal).Replace("mm", "", StringComparison.OrdinalIgnoreCase);
        if (cleaned.Contains('.') && !cleaned.Contains(','))
        {
            var parts = cleaned.Split('.');
            if (parts[0].Length > 0
                && parts[0].All(char.IsDigit)
                && parts.Skip(1).All(part => part.Length == 3 && part.All(char.IsDigit)))
            {
                cleaned = string.Concat(parts);
            }
        }

        if (int.TryParse(cleaned, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
        {
            return value;
        }

        issues.Add(new CatalogIssue(field, CatalogIssueKind.InvalidNumber));
        return null;
    }

    private static int? OptionalInt(string? text, List<CatalogIssue> issues)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        if (int.TryParse(text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
        {
            return value;
        }

        issues.Add(new CatalogIssue(CatalogField.WarrantyMonths, CatalogIssueKind.InvalidNumber));
        return null;
    }

    private static bool TryParse(string? text, bool money, out decimal? amount)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            amount = null;
            return true;
        }

        var cleaned = text.Trim();
        if (money)
        {
            cleaned = cleaned.Replace("VNĐ", "", StringComparison.OrdinalIgnoreCase)
                .Replace("đ", "", StringComparison.Ordinal)
                .Trim();
        }

        cleaned = cleaned.Replace(" ", "", StringComparison.Ordinal);
        if (cleaned.Contains(',') && cleaned.Contains('.'))
        {
            if (cleaned.LastIndexOf(',') > cleaned.LastIndexOf('.'))
            {
                cleaned = cleaned.Replace(".", "", StringComparison.Ordinal).Replace(',', '.');
            }
            else
            {
                cleaned = cleaned.Replace(",", "", StringComparison.Ordinal);
            }
        }
        else if (cleaned.Contains(','))
        {
            cleaned = cleaned.Replace(',', '.');
        }
        else if (cleaned.Count(character => character == '.') > 1 || (money && IsGrouped(cleaned)))
        {
            cleaned = cleaned.Replace(".", "", StringComparison.Ordinal);
        }

        if (!decimal.TryParse(cleaned, NumberStyles.Number, CultureInfo.InvariantCulture, out var value))
        {
            amount = null;
            return false;
        }

        amount = value;
        return true;
    }

    private static bool IsGrouped(string cleaned)
    {
        var parts = cleaned.Split('.');
        if (parts.Length < 2 || parts[0].Length == 0)
        {
            return false;
        }

        for (var index = 1; index < parts.Length; index++)
        {
            if (parts[index].Length != 3 || !parts[index].All(char.IsDigit))
            {
                return false;
            }
        }

        return parts[0].All(character => char.IsDigit(character) || character == '-');
    }
}

public sealed class CatalogItemForm
{
    public string? Code { get; set; }

    public string? Name { get; set; }

    public string? Category { get; set; }

    public string? Brand { get; set; }

    public string? Origin { get; set; }

    public string? Unit { get; set; }

    public string? WeightKg { get; set; }

    public string? VolumeM3 { get; set; }

    public string? WarrantyMonths { get; set; }

    public string? MinimumStock { get; set; }

    public string? HeightMm { get; set; }

    public string? LengthMm { get; set; }

    public string? WidthMm { get; set; }

    public string? CostPrice { get; set; }

    public string? RetailPrice { get; set; }

    public string? DealerPrice { get; set; }

    public string? DistributorPrice { get; set; }

    public string? ProjectPrice { get; set; }

    public string? DealerDiscountPercent { get; set; }

    public string? DistributorDiscountPercent { get; set; }

    public string? ProjectDiscountPercent { get; set; }

    public int Version { get; set; }
}

public sealed class CatalogPriceForm
{
    public Guid Id { get; set; }

    public bool IncludeDetails { get; set; }

    public string? Code { get; set; }

    public string? Name { get; set; }

    public string? Category { get; set; }

    public string? Brand { get; set; }

    public string? Origin { get; set; }

    public string? Unit { get; set; }

    public string? WeightKg { get; set; }

    public string? VolumeM3 { get; set; }

    public string? WarrantyMonths { get; set; }

    public string? MinimumStock { get; set; }

    public string? HeightMm { get; set; }

    public string? LengthMm { get; set; }

    public string? WidthMm { get; set; }

    public string? CostPrice { get; set; }

    public string? RetailPrice { get; set; }

    public string? DealerPrice { get; set; }

    public string? DistributorPrice { get; set; }

    public string? ProjectPrice { get; set; }

    public string? DealerDiscountPercent { get; set; }

    public string? DistributorDiscountPercent { get; set; }

    public string? ProjectDiscountPercent { get; set; }

    public int Version { get; set; }
}
