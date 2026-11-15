using Pancras.Domain.Purchasing;

namespace Pancras.Domain.Inventory;

public enum StockDocumentKind
{
    Stocktake = 1,
    Adjustment = 2,
    Disposal = 3
}

/// <summary>
/// Ảnh hàng, biên bản, chứng từ đính kèm biên bản kiểm kê, phiếu điều chỉnh hoặc phiếu hủy.
/// Nội dung file nằm ở kho lưu trữ, không nằm trong DB. Không có khóa ngoại vì một bảng phục vụ ba loại phiếu.
/// </summary>
public sealed class StockDocumentAttachment
{
    private StockDocumentAttachment()
    {
        FileName = string.Empty;
        StoredName = string.Empty;
        ContentType = string.Empty;
        UploadedBy = string.Empty;
    }

    public Guid Id { get; private set; }

    public StockDocumentKind Kind { get; private set; }

    public Guid DocumentId { get; private set; }

    public string FileName { get; private set; }

    public string StoredName { get; private set; }

    public string ContentType { get; private set; }

    public long SizeBytes { get; private set; }

    public DateTimeOffset UploadedAt { get; private set; }

    public string UploadedBy { get; private set; }

    public static StockDocumentAttachment? TryCreate(
        StockDocumentKind kind,
        Guid documentId,
        string? fileName,
        string storedName,
        long sizeBytes,
        DateTimeOffset uploadedAt,
        string? uploadedBy)
    {
        var clean = AttachmentRules.CleanFileName(fileName);
        if (documentId == Guid.Empty
            || !Enum.IsDefined(kind)
            || clean is null
            || !AttachmentRules.IsSafeStoredName(storedName)
            || sizeBytes <= 0
            || sizeBytes > AttachmentRules.MaxBytes)
        {
            return null;
        }

        var by = (uploadedBy ?? string.Empty).Trim();
        return new StockDocumentAttachment
        {
            Id = Guid.CreateVersion7(),
            Kind = kind,
            DocumentId = documentId,
            FileName = clean,
            StoredName = storedName,
            ContentType = AttachmentRules.ContentTypeFor(Path.GetExtension(clean))!,
            SizeBytes = sizeBytes,
            UploadedAt = uploadedAt,
            UploadedBy = by.Length > PurchaseAttachment.UploadedByMaxLength ? by[..PurchaseAttachment.UploadedByMaxLength] : by
        };
    }
}
