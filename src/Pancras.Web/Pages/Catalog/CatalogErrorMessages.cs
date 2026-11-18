using Pancras.Application.Common;

namespace Pancras.Web.Pages.Catalog;

public static class CatalogErrorMessages
{
    public static IReadOnlyList<string> Describe(CatalogError error, IReadOnlyList<CatalogIssue> issues, string? code)
    {
        if (error == CatalogError.PriceListForbidden)
        {
            return ["Bạn không được sửa bảng giá."];
        }

        if (error == CatalogError.NotFound)
        {
            return ["Không tìm thấy mặt hàng này."];
        }

        if (error == CatalogError.Forbidden)
        {
            return ["Bạn không có quyền thực hiện thao tác này."];
        }

        if (error == CatalogError.CostLocked)
        {
            return ["Giá vốn không sửa được khi mặt hàng đã có tồn. Giá vốn cập nhật theo nhập kho."];
        }

        if (error == CatalogError.VersionConflict)
        {
            return ["Sản phẩm vừa được người khác sửa. Tải lại và thử lại."];
        }

        var messages = issues.Select(issue => Sentence(issue, code)).Distinct().ToArray();
        if (messages.Length > 0)
        {
            return messages;
        }

        return error switch
        {
            CatalogError.Incomplete => ["Cần nhập mã, tên, ngành và đủ năm mức giá."],
            CatalogError.TooLong => ["Một mục dài hơn mức cho phép."],
            CatalogError.Negative => ["Giá và số đo không được âm."],
            CatalogError.TooLarge => ["Một số vượt quá giới hạn cho phép."],
            CatalogError.DuplicateCode => ["Mã mặt hàng đã có."],
            _ => []
        };
    }

    private static string Sentence(CatalogIssue issue, string? code)
    {
        var label = Label(issue.Field);
        return issue.Kind switch
        {
            CatalogIssueKind.Missing => issue.Field == CatalogField.Category
                ? "Chọn ngành hàng."
                : $"{label} chưa được nhập.",
            CatalogIssueKind.TooLong => $"{label} chỉ được tối đa {issue.Limit} ký tự.",
            CatalogIssueKind.Negative => $"{label} không được âm.",
            CatalogIssueKind.InvalidNumber => $"{label} không phải số.",
            CatalogIssueKind.TooLarge => $"{label} vượt quá giới hạn cho phép.",
            CatalogIssueKind.Duplicate =>
                $"Mã mặt hàng \"{code?.Trim()}\" đã có. Mã không phân biệt chữ hoa và chữ thường.",
            _ => $"{label} không hợp lệ."
        };
    }

    private static string Label(CatalogField field) => field switch
    {
        CatalogField.Code => "Mã mặt hàng",
        CatalogField.Name => "Tên mặt hàng",
        CatalogField.Category => "Ngành hàng",
        CatalogField.Brand => "Thương hiệu",
        CatalogField.Origin => "Xuất xứ",
        CatalogField.Unit => "Đơn vị tính",
        CatalogField.Weight => "Trọng lượng",
        CatalogField.Volume => "Thể tích",
        CatalogField.WarrantyMonths => "Số tháng bảo hành",
        CatalogField.MinimumStock => "Mức tồn tối thiểu",
        CatalogField.Height => "Chiều cao (mm)",
        CatalogField.Length => "Chiều dài (mm)",
        CatalogField.Width => "Chiều rộng (mm)",
        CatalogField.Image => "Ảnh sản phẩm",
        CatalogField.CostPrice => "Giá vốn",
        CatalogField.RetailPrice => "Giá lẻ",
        CatalogField.DealerPrice => "Giá đại lý",
        CatalogField.DistributorPrice => "Giá nhà phân phối",
        CatalogField.ProjectPrice => "Giá dự án",
        CatalogField.DealerDiscountPercent => "% chiết khấu giá đại lý",
        CatalogField.DistributorDiscountPercent => "% chiết khấu giá nhà phân phối",
        CatalogField.ProjectDiscountPercent => "% chiết khấu giá dự án",
        _ => "Mục"
    };
}
