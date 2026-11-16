using Pancras.Domain.Catalog;

namespace Pancras.Application.Catalog;

public sealed record CatalogSlice(IReadOnlyList<CatalogItem> Items, int TotalCount, int PageNumber);

public sealed record CatalogItemMeasure(Guid Id, decimal? WeightKg, decimal? VolumeM3);

public sealed record CatalogItemRow(
    Guid Id,
    string Code,
    string Name,
    ProductCategory Category,
    string Brand,
    string Origin,
    string Unit,
    decimal? WeightKg,
    decimal? VolumeM3,
    int? WarrantyMonths,
    decimal? MinimumStock,
    decimal CostPrice,
    decimal RetailPrice,
    decimal DealerPrice,
    decimal DistributorPrice,
    decimal ProjectPrice,
    int? HeightMm,
    int? LengthMm,
    int? WidthMm,
    string ImagePath,
    int Version = 0,
    Pancras.Application.Auditing.AuditStampView? Stamp = null,
    bool IsActive = true)
{
    public static CatalogItemRow From(CatalogItem item) =>
        new(
            item.Id,
            item.Code,
            item.Name,
            item.Category,
            item.Brand,
            item.Origin,
            item.Unit,
            item.WeightKg,
            item.VolumeM3,
            item.WarrantyMonths,
            item.MinimumStock,
            item.CostPrice,
            item.RetailPrice,
            item.DealerPrice,
            item.DistributorPrice,
            item.ProjectPrice,
            item.HeightMm,
            item.LengthMm,
            item.WidthMm,
            item.ImagePath,
            item.Version,
            Pancras.Application.Auditing.AuditStampView.From(item),
            item.IsActive);
}

public static class ProductCategoryToken
{
    public const string Sanitary = "thiet-bi-ve-sinh";

    public const string Bathroom = "noi-that-phong-tam";

    public const string Building = "vat-tu-xay-dung";

    public static string From(ProductCategory category) => category switch
    {
        ProductCategory.SanitaryEquipment => Sanitary,
        ProductCategory.BathroomFurniture => Bathroom,
        ProductCategory.BuildingMaterials => Building,
        _ => string.Empty
    };

    public static bool TryParse(string? token, out ProductCategory category)
    {
        switch (token?.Trim())
        {
            case Sanitary:
                category = ProductCategory.SanitaryEquipment;
                return true;
            case Bathroom:
                category = ProductCategory.BathroomFurniture;
                return true;
            case Building:
                category = ProductCategory.BuildingMaterials;
                return true;
            default:
                category = default;
                return false;
        }
    }
}

public enum CatalogStatusFilter
{
    Active,
    Disabled,
    All
}

public sealed record ListCatalogItemsQuery(
    ProductCategory? Category,
    bool CategoryRejected,
    string? Term,
    CatalogStatusFilter Status = CatalogStatusFilter.Active,
    Pancras.Application.Common.ListSort? Sort = null,
    int PageNumber = 1,
    int PageSize = Pancras.Application.Common.PageSizes.Default)
{
    /// <summary>Cùng bộ lọc, đổi trang và cỡ trang (số trang < 1 về 1, cỡ trang < 1 về mặc định).</summary>
    public ListCatalogItemsQuery WithPage(int? number, int? size) =>
        this with { PageNumber = number is > 0 ? number.Value : 1, PageSize = size is > 0 ? size.Value : Pancras.Application.Common.PageSizes.Default };

    public Pancras.Application.Common.ListSort Order => Sort ?? CatalogOrdering.Parse(null);

    public static CatalogStatusFilter ParseStatus(string? token) => token?.Trim().ToLowerInvariant() switch
    {
        "disabled" => CatalogStatusFilter.Disabled,
        "all" => CatalogStatusFilter.All,
        _ => CatalogStatusFilter.Active
    };

    public static ListCatalogItemsQuery From(string? categoryToken, string? term, string? statusToken = null, string? sortToken = null)
    {
        var normalizedTerm = Normalize(term);
        var status = ParseStatus(statusToken);
        var sort = CatalogOrdering.Parse(sortToken);
        if (string.IsNullOrWhiteSpace(categoryToken))
        {
            return new ListCatalogItemsQuery(null, false, normalizedTerm, status, sort);
        }

        if (ProductCategoryToken.TryParse(categoryToken, out var category))
        {
            return new ListCatalogItemsQuery(category, false, normalizedTerm, status, sort);
        }

        return new ListCatalogItemsQuery(null, true, normalizedTerm, status, sort);
    }

    public bool Matches(CatalogItem item)
    {
        if (item.IsCombo || CategoryRejected)
        {
            return false;
        }

        if (Category is ProductCategory category && item.Category != category)
        {
            return false;
        }

        if ((Status == CatalogStatusFilter.Active && !item.IsActive) || (Status == CatalogStatusFilter.Disabled && item.IsActive))
        {
            return false;
        }

        if (Term is null)
        {
            return true;
        }

        return item.Code.Contains(Term, StringComparison.OrdinalIgnoreCase)
            || item.Name.Contains(Term, StringComparison.OrdinalIgnoreCase)
            || item.Brand.Contains(Term, StringComparison.OrdinalIgnoreCase);
    }

    private static string? Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        return trimmed.Length == 0 ? null : trimmed;
    }
}

public sealed record CatalogItemPage(
    IReadOnlyList<CatalogItemRow> Rows,
    int PageNumber,
    int PageSize,
    int TotalCount)
{
    public int TotalPages => TotalCount == 0 ? 1 : (TotalCount + PageSize - 1) / PageSize;

    public bool HasPrevious => PageNumber > 1;

    public bool HasNext => PageNumber < TotalPages;
}

public sealed class CreateCatalogItemCommand
{
    public string? Code { get; set; }

    public string? Name { get; set; }

    public ProductCategory? Category { get; set; }

    public string? Brand { get; set; }

    public string? Origin { get; set; }

    public string? Unit { get; set; }

    public decimal? WeightKg { get; set; }

    public decimal? VolumeM3 { get; set; }

    public int? WarrantyMonths { get; set; }

    public decimal? MinimumStock { get; set; }

    public int? HeightMm { get; set; }

    public int? LengthMm { get; set; }

    public int? WidthMm { get; set; }

    public string? ImagePath { get; set; }

    public decimal? CostPrice { get; set; }

    public decimal? RetailPrice { get; set; }

    public decimal? DealerPrice { get; set; }

    public decimal? DistributorPrice { get; set; }

    public decimal? ProjectPrice { get; set; }
}

public sealed record CreatedCatalogItem(Guid Id, string Code, string Name, ProductCategory Category);

public sealed class UpdateCatalogPricesCommand
{
    public Guid Id { get; set; }

    public string? Role { get; set; }

    public decimal? CostPrice { get; set; }

    public decimal? RetailPrice { get; set; }

    public decimal? DealerPrice { get; set; }

    public decimal? DistributorPrice { get; set; }

    public decimal? ProjectPrice { get; set; }

    public bool IncludeDetails { get; set; }

    public string? Code { get; set; }

    public string? Name { get; set; }

    public ProductCategory? Category { get; set; }

    public string? Brand { get; set; }

    public string? Origin { get; set; }

    public string? Unit { get; set; }

    public decimal? WeightKg { get; set; }

    public decimal? VolumeM3 { get; set; }

    public int? WarrantyMonths { get; set; }

    public decimal? MinimumStock { get; set; }

    public int? HeightMm { get; set; }

    public int? LengthMm { get; set; }

    public int? WidthMm { get; set; }

    public string? ImagePath { get; set; }

    public bool ReplaceImage { get; set; }

    public int Version { get; set; }
}

public sealed record ResolveSalePriceCommand(
    Guid ItemId,
    SaleCustomerKind Customer,
    bool FromQuoteFile,
    decimal? FilePrice);
