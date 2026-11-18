using Pancras.Domain.Catalog;

namespace Pancras.Web.Formatting;

public static class CatalogLabels
{
    public const string Missing = "Chưa ghi";

    public const string PricesIncludeVat = "Giá đã gồm VAT.";

    public static string Category(ProductCategory category) => category switch
    {
        ProductCategory.SanitaryEquipment => "Thiết bị vệ sinh",
        ProductCategory.BathroomFurniture => "Nội thất phòng tắm",
        ProductCategory.BuildingMaterials => "Vật tư xây dựng",
        _ => Missing
    };

    public static string Text(string? value) => string.IsNullOrWhiteSpace(value) ? Missing : value;

    /// <summary>% chiết khấu mặc định, hoặc dấu gạch khi chưa cấu hình.</summary>
    public static string Percent(decimal? value) => value is null ? Missing : VndMoney.FormatPercent(value) + " %";

    public static string Measure(decimal? value) => value is null ? Missing : VndMoney.FormatMeasure(value.Value);

    public static string Months(int? value) => value is null ? Missing : value.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);

    public static string Millimeters(int? value) => value is null ? Missing : value.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
}
