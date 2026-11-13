using Pancras.Domain.Purchasing;

namespace Pancras.Domain.Inventory;

/// <summary>Biên bản, ảnh hàng, chứng từ đính kèm phiếu chuyển kho. Nội dung file nằm ở kho lưu trữ, không nằm trong DB.</summary>
public sealed class TransferAttachment
{
    private TransferAttachment()
    {
        FileName = string.Empty;
        StoredName = string.Empty;
        ContentType = string.Empty;
        UploadedBy = string.Empty;
    }

    public Guid Id { get; private set; }

    public Guid TransferSlipId { get; private set; }

    public string FileName { get; private set; }

    public string StoredName { get; private set; }

    public string ContentType { get; private set; }

    public long SizeBytes { get; private set; }

    public DateTimeOffset UploadedAt { get; private set; }

    public string UploadedBy { get; private set; }

    public static TransferAttachment? TryCreate(
        Guid transferSlipId,
        string? fileName,
        string storedName,
        long sizeBytes,
        DateTimeOffset uploadedAt,
        string? uploadedBy)
    {
        var clean = AttachmentRules.CleanFileName(fileName);
        if (transferSlipId == Guid.Empty
            || clean is null
            || !AttachmentRules.IsSafeStoredName(storedName)
            || sizeBytes <= 0
            || sizeBytes > AttachmentRules.MaxBytes)
        {
            return null;
        }

        var by = (uploadedBy ?? string.Empty).Trim();
        return new TransferAttachment
        {
            Id = Guid.CreateVersion7(),
            TransferSlipId = transferSlipId,
            FileName = clean,
            StoredName = storedName,
            ContentType = AttachmentRules.ContentTypeFor(Path.GetExtension(clean))!,
            SizeBytes = sizeBytes,
            UploadedAt = uploadedAt,
            UploadedBy = by.Length > PurchaseAttachment.UploadedByMaxLength ? by[..PurchaseAttachment.UploadedByMaxLength] : by
        };
    }
}
