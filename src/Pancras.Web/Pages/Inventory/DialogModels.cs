using Pancras.Application.Inventory;

namespace Pancras.Web.Pages.Inventory;

/// <summary>Ô chọn địa điểm và mặt hàng của hộp thoại lập phiếu; giữ lại giá trị đã chọn khi máy chủ mở lại hộp thoại vì lỗi.</summary>
public sealed record LocationItemFields(
    string Prefix,
    IReadOnlyList<StockLocationChoice> Locations,
    IReadOnlyList<StockItemChoice> Items,
    Guid SelectedLocation,
    Guid SelectedItem,
    string LocationLabel = "Địa điểm",
    string? LocationSelectId = null,
    string? ItemSelectId = null);

/// <summary>Ô lý do, chứng từ và tệp đính kèm của hộp thoại lập phiếu.</summary>
public sealed record ReasonAndFiles(string Prefix, string? Reason, string? DocumentRef, string ReasonLabel = "Lý do", string FilesLabel = "Ảnh hàng, chứng từ (file đính kèm)");
