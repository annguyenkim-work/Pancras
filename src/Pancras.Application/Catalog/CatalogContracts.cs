using Pancras.Domain.Catalog;

namespace Pancras.Application.Catalog;

public interface ICatalogItemRepository
{
    Task<CatalogSlice> ListAsync(
        CatalogListFilter filter,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<CatalogItem>> ListMatchingAsync(CatalogListFilter filter, CancellationToken cancellationToken);

    Task<CatalogItem?> GetVisibleReadOnlyAsync(Guid id, CancellationToken cancellationToken);

    Task<CatalogItem?> GetVisibleForUpdateAsync(Guid id, CancellationToken cancellationToken);

    Task<CatalogItem?> GetByIdReadOnlyAsync(Guid id, CancellationToken cancellationToken);

    Task<bool> NormalizedCodeExistsAsync(string normalizedCode, CancellationToken cancellationToken);

    Task<bool> TryAddAsync(CatalogItem item, CancellationToken cancellationToken);

    Task<bool> TrySaveAsync(CancellationToken cancellationToken);

    Task<IReadOnlyDictionary<Guid, CatalogItemMeasure>> ListMeasuresAsync(
        IReadOnlyCollection<Guid> ids,
        CancellationToken cancellationToken);
}

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

public sealed record CatalogListFilter(
    ProductCategory? Category,
    bool CategoryRejected,
    string? Term,
    CatalogStatusFilter Status = CatalogStatusFilter.Active)
{
    public static CatalogStatusFilter ParseStatus(string? token) => token?.Trim().ToLowerInvariant() switch
    {
        "disabled" => CatalogStatusFilter.Disabled,
        "all" => CatalogStatusFilter.All,
        _ => CatalogStatusFilter.Active
    };

    public static CatalogListFilter From(string? categoryToken, string? term, string? statusToken = null)
    {
        var normalizedTerm = Normalize(term);
        var status = ParseStatus(statusToken);
        if (string.IsNullOrWhiteSpace(categoryToken))
        {
            return new CatalogListFilter(null, false, normalizedTerm, status);
        }

        if (ProductCategoryToken.TryParse(categoryToken, out var category))
        {
            return new CatalogListFilter(category, false, normalizedTerm, status);
        }

        return new CatalogListFilter(null, true, normalizedTerm, status);
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

public sealed record CatalogPageRequest(int Number, int Size)
{
    public const int DefaultSize = 20;

    public static CatalogPageRequest First { get; } = new(1, DefaultSize);

    public static CatalogPageRequest From(int? page, int? size = null) =>
        new(page is > 0 ? page.Value : 1, Pancras.Application.Common.PageSizes.Normalize(size));
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
