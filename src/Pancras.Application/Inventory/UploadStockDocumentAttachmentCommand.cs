using Pancras.Domain.Employees;
using Pancras.Domain.Inventory;

namespace Pancras.Application.Inventory;

public sealed class UploadStockDocumentAttachmentCommand
{
    public StockDocumentKind Kind { get; set; }

    public Guid DocumentId { get; set; }

    public string? FileName { get; set; }

    public Stream? Content { get; set; }

    public EmployeeActor Actor { get; set; } = new(Guid.Empty, null, null);
}

/// <summary>Tra kho của một biên bản kiểm kê, phiếu điều chỉnh hoặc phiếu hủy để kiểm quyền theo kho.</summary>
internal static class StockDocumentLookup
{
    // Chỉ đọc, không dùng FindXxxForUpdateAsync: hàm đó mở transaction giữ đến CommitAsync, mà nhánh lưu file không commit nên file bị rollback.
    public static Task<Guid?> LocationOfAsync(IInventoryRepository repository, StockDocumentKind kind, Guid documentId, CancellationToken cancellationToken) =>
        repository.FindDocumentLocationAsync(kind, documentId, cancellationToken);

    public static bool CanTouch(EmployeeActor actor, Guid locationId) =>
        InventoryAccess.CanOperate(actor.Role) && InventoryAccess.CanAccessLocation(actor, locationId);
}
