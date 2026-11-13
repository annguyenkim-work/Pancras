namespace Pancras.Domain.Catalog;

public sealed class CatalogItem : Pancras.Domain.Auditing.AuditStamped
{
    public const int CodeMaxLength = 32;

    public const int NameMaxLength = 200;

    public const int BrandMaxLength = 100;

    public const int OriginMaxLength = 100;

    public const int UnitMaxLength = 32;

    public const int ImagePathMaxLength = 240;

    public const bool SellingPricesIncludeVat = true;

    private CatalogItem()
    {
        Code = string.Empty;
        NormalizedCode = string.Empty;
        Name = string.Empty;
        Brand = string.Empty;
        Origin = string.Empty;
        Unit = string.Empty;
        ImagePath = string.Empty;
    }

    public Guid Id { get; private set; }

    public string Code { get; private set; }

    public string NormalizedCode { get; private set; }

    public string Name { get; private set; }

    public ProductCategory Category { get; private set; }

    public string Brand { get; private set; }

    public string Origin { get; private set; }

    public string Unit { get; private set; }

    public decimal? WeightKg { get; private set; }

    public decimal? VolumeM3 { get; private set; }

    public int? HeightMm { get; private set; }

    public int? LengthMm { get; private set; }

    public int? WidthMm { get; private set; }

    public string ImagePath { get; private set; }

    public int? WarrantyMonths { get; private set; }

    public decimal? MinimumStock { get; private set; }

    public decimal CostPrice { get; private set; }

    public decimal RetailPrice { get; private set; }

    public decimal DealerPrice { get; private set; }

    public decimal DistributorPrice { get; private set; }

    public decimal ProjectPrice { get; private set; }

    public bool IsCombo { get; private set; }

    public static string NormalizeCode(string code) => code.Trim().ToUpperInvariant();

    public static CatalogItem Create(
        string code,
        string name,
        ProductCategory category,
        string? brand,
        string? origin,
        string? unit,
        decimal? weightKg,
        decimal? volumeM3,
        int? warrantyMonths,
        decimal? minimumStock,
        decimal costPrice,
        decimal retailPrice,
        decimal dealerPrice,
        decimal distributorPrice,
        decimal projectPrice,
        int? heightMm = null,
        int? lengthMm = null,
        int? widthMm = null,
        string? imagePath = null) =>
        Build(
            false,
            code,
            name,
            category,
            brand,
            origin,
            unit,
            weightKg,
            volumeM3,
            warrantyMonths,
            minimumStock,
            costPrice,
            retailPrice,
            dealerPrice,
            distributorPrice,
            projectPrice,
            heightMm,
            lengthMm,
            widthMm,
            imagePath);

    public static CatalogItem CreateCombo(
        string code,
        string name,
        ProductCategory category,
        string? brand,
        string? origin,
        string? unit,
        decimal? weightKg,
        decimal? volumeM3,
        int? warrantyMonths,
        decimal? minimumStock,
        decimal costPrice,
        decimal retailPrice,
        decimal dealerPrice,
        decimal distributorPrice,
        decimal projectPrice,
        int? heightMm = null,
        int? lengthMm = null,
        int? widthMm = null,
        string? imagePath = null) =>
        Build(
            true,
            code,
            name,
            category,
            brand,
            origin,
            unit,
            weightKg,
            volumeM3,
            warrantyMonths,
            minimumStock,
            costPrice,
            retailPrice,
            dealerPrice,
            distributorPrice,
            projectPrice,
            heightMm,
            lengthMm,
            widthMm,
            imagePath);

    public void UpdatePrices(
        decimal costPrice,
        decimal retailPrice,
        decimal dealerPrice,
        decimal distributorPrice,
        decimal projectPrice)
    {
        var cost = WholeDong(costPrice);
        var retail = WholeDong(retailPrice);
        var dealer = WholeDong(dealerPrice);
        var distributor = WholeDong(distributorPrice);
        var project = WholeDong(projectPrice);
        CostPrice = cost;
        RetailPrice = retail;
        DealerPrice = dealer;
        DistributorPrice = distributor;
        ProjectPrice = project;
    }

    public void SetCostPrice(decimal costPrice) => CostPrice = WholeDong(costPrice);

    public void SetImage(string? imagePath) => ImagePath = OptionalText(imagePath, ImagePathMaxLength);

    public void UpdateDetails(
        string code,
        string name,
        ProductCategory category,
        string? brand,
        string? origin,
        string? unit,
        decimal? weightKg,
        decimal? volumeM3,
        int? warrantyMonths,
        decimal? minimumStock,
        int? heightMm = null,
        int? lengthMm = null,
        int? widthMm = null)
    {
        var storedCode = RequiredText(code, CodeMaxLength);
        var normalized = NormalizeCode(storedCode);
        if (normalized.Length > CodeMaxLength)
        {
            throw new ArgumentException("Code is too long.", nameof(code));
        }

        if (!Enum.IsDefined(category))
        {
            throw new ArgumentOutOfRangeException(nameof(category));
        }

        if (warrantyMonths is < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(warrantyMonths));
        }

        var storedName = RequiredText(name, NameMaxLength);
        var storedBrand = OptionalText(brand, BrandMaxLength);
        var storedOrigin = OptionalText(origin, OriginMaxLength);
        var storedUnit = OptionalText(unit, UnitMaxLength);
        var weight = Measure(weightKg);
        var volume = Measure(volumeM3);
        var stock = Measure(minimumStock);
        var height = Millimeter(heightMm);
        var length = Millimeter(lengthMm);
        var width = Millimeter(widthMm);
        Code = storedCode;
        NormalizedCode = normalized;
        Name = storedName;
        Category = category;
        Brand = storedBrand;
        Origin = storedOrigin;
        Unit = storedUnit;
        WeightKg = weight;
        VolumeM3 = volume;
        HeightMm = height;
        LengthMm = length;
        WidthMm = width;
        WarrantyMonths = warrantyMonths;
        MinimumStock = stock;
    }

    private static CatalogItem Build(
        bool isCombo,
        string code,
        string name,
        ProductCategory category,
        string? brand,
        string? origin,
        string? unit,
        decimal? weightKg,
        decimal? volumeM3,
        int? warrantyMonths,
        decimal? minimumStock,
        decimal costPrice,
        decimal retailPrice,
        decimal dealerPrice,
        decimal distributorPrice,
        decimal projectPrice,
        int? heightMm,
        int? lengthMm,
        int? widthMm,
        string? imagePath)
    {
        var storedCode = RequiredText(code, CodeMaxLength);
        var normalized = NormalizeCode(storedCode);
        if (normalized.Length > CodeMaxLength)
        {
            throw new ArgumentException("Code is too long.", nameof(code));
        }

        if (!Enum.IsDefined(category))
        {
            throw new ArgumentOutOfRangeException(nameof(category));
        }

        if (warrantyMonths is < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(warrantyMonths));
        }

        return new CatalogItem
        {
            Id = Guid.CreateVersion7(),
            Code = storedCode,
            NormalizedCode = normalized,
            Name = RequiredText(name, NameMaxLength),
            Category = category,
            Brand = OptionalText(brand, BrandMaxLength),
            Origin = OptionalText(origin, OriginMaxLength),
            Unit = OptionalText(unit, UnitMaxLength),
            WeightKg = Measure(weightKg),
            VolumeM3 = Measure(volumeM3),
            HeightMm = Millimeter(heightMm),
            LengthMm = Millimeter(lengthMm),
            WidthMm = Millimeter(widthMm),
            ImagePath = OptionalText(imagePath, ImagePathMaxLength),
            WarrantyMonths = warrantyMonths,
            MinimumStock = Measure(minimumStock),
            CostPrice = WholeDong(costPrice),
            RetailPrice = WholeDong(retailPrice),
            DealerPrice = WholeDong(dealerPrice),
            DistributorPrice = WholeDong(distributorPrice),
            ProjectPrice = WholeDong(projectPrice),
            IsCombo = isCombo
        };
    }

    private static string RequiredText(string? value, int maxLength)
    {
        var trimmed = value?.Trim() ?? string.Empty;
        if (trimmed.Length == 0 || trimmed.Length > maxLength)
        {
            throw new ArgumentException("Text is missing or too long.");
        }

        return trimmed;
    }

    private static string OptionalText(string? value, int maxLength)
    {
        var trimmed = value?.Trim() ?? string.Empty;
        if (trimmed.Length > maxLength)
        {
            throw new ArgumentException("Text is too long.");
        }

        return trimmed;
    }

    private static int? Millimeter(int? value)
    {
        if (value is null)
        {
            return null;
        }

        if (value.Value < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(value));
        }

        return value;
    }

    private static decimal? Measure(decimal? value)
    {
        if (value is null)
        {
            return null;
        }

        if (value.Value < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(value));
        }

        return decimal.Round(value.Value, 3, MidpointRounding.AwayFromZero);
    }

    private static decimal WholeDong(decimal value)
    {
        if (value < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(value));
        }

        return decimal.Round(value, 0, MidpointRounding.AwayFromZero);
    }
}
