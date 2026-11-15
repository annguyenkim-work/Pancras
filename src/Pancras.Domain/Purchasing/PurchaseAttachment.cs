namespace Pancras.Domain.Purchasing;

public enum AttachmentKind
{
    CustomsDeclaration = 1,
    SupplierInvoice = 2,
    BillOfLading = 3,
    PackingList = 4,
    LogisticsInvoice = 5,
    Other = 6
}

/// <summary>Chứng từ đính kèm của đơn mua (tờ khai, hóa đơn, vận đơn...). Nội dung file nằm ở kho lưu trữ, không nằm trong DB.</summary>
public sealed class PurchaseAttachment
{
    public const int FileNameMaxLength = 200;

    public const int UploadedByMaxLength = 64;

    private PurchaseAttachment()
    {
        FileName = string.Empty;
        StoredName = string.Empty;
        ContentType = string.Empty;
        UploadedBy = string.Empty;
    }

    public Guid Id { get; private set; }

    public Guid PurchaseOrderId { get; private set; }

    public AttachmentKind Kind { get; private set; }

    /// <summary>Tên file người dùng thấy (đã làm sạch).</summary>
    public string FileName { get; private set; }

    /// <summary>Tên file trong kho lưu trữ, do hệ thống sinh, không lấy từ người dùng.</summary>
    public string StoredName { get; private set; }

    public string ContentType { get; private set; }

    public long SizeBytes { get; private set; }

    public DateTimeOffset UploadedAt { get; private set; }

    public string UploadedBy { get; private set; }

    public static PurchaseAttachment? TryCreate(
        Guid orderId,
        AttachmentKind kind,
        string? fileName,
        string storedName,
        long sizeBytes,
        DateTimeOffset uploadedAt,
        string? uploadedBy)
    {
        var clean = AttachmentRules.CleanFileName(fileName);
        if (orderId == Guid.Empty
            || !Enum.IsDefined(kind)
            || clean is null
            || !AttachmentRules.IsSafeStoredName(storedName)
            || sizeBytes <= 0
            || sizeBytes > AttachmentRules.MaxBytes)
        {
            return null;
        }

        var by = (uploadedBy ?? string.Empty).Trim();
        return new PurchaseAttachment
        {
            Id = Guid.CreateVersion7(),
            PurchaseOrderId = orderId,
            Kind = kind,
            FileName = clean,
            StoredName = storedName,
            ContentType = AttachmentRules.ContentTypeFor(Path.GetExtension(clean))!,
            SizeBytes = sizeBytes,
            UploadedAt = uploadedAt,
            UploadedBy = by.Length > UploadedByMaxLength ? by[..UploadedByMaxLength] : by
        };
    }
}

public static class AttachmentRules
{
    public const long MaxBytes = 10L * 1024 * 1024;

    public const int MaxPerOrder = 30;

    private static readonly Dictionary<string, string> ContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        [".pdf"] = "application/pdf",
        [".jpg"] = "image/jpeg",
        [".jpeg"] = "image/jpeg",
        [".png"] = "image/png",
        [".xlsx"] = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        [".docx"] = "application/vnd.openxmlformats-officedocument.wordprocessingml.document"
    };

    public static string AllowedExtensionsText => "PDF, JPG, PNG, Excel (xlsx), Word (docx)";

    public static string? ContentTypeFor(string? extension) =>
        extension is not null && ContentTypes.TryGetValue(extension, out var type) ? type : null;

    /// <summary>Bỏ đường dẫn và ký tự nguy hiểm; trả về null nếu tên rỗng hoặc đuôi file không được phép.</summary>
    public static string? CleanFileName(string? fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return null;
        }

        var name = fileName.Replace('\\', '/');
        name = name[(name.LastIndexOf('/') + 1)..];
        var cleaned = new string(name.Where(character => !char.IsControl(character) && "<>:\"|?*".IndexOf(character) < 0).ToArray()).Trim().TrimEnd('.');
        var extension = Path.GetExtension(cleaned);
        if (cleaned.Length == 0 || ContentTypeFor(extension) is null)
        {
            return null;
        }

        if (cleaned.Length > PurchaseAttachment.FileNameMaxLength)
        {
            var stem = Path.GetFileNameWithoutExtension(cleaned);
            cleaned = stem[..(PurchaseAttachment.FileNameMaxLength - extension.Length)] + extension;
        }

        return cleaned;
    }

    /// <summary>Tên lưu trữ hợp lệ: 32 ký tự hex và một đuôi file được phép. Chặn mọi đường dẫn.</summary>
    public static bool IsSafeStoredName(string? storedName)
    {
        if (string.IsNullOrEmpty(storedName) || storedName.Length < 34 || storedName.Length > 40)
        {
            return false;
        }

        for (var index = 0; index < 32; index++)
        {
            if (!Uri.IsHexDigit(storedName[index]))
            {
                return false;
            }
        }

        return storedName[32] == '.' && ContentTypeFor(storedName[32..]) is not null;
    }

    /// <summary>Kiểm tra vài byte đầu của file khớp với đuôi file, để không nhận file thực thi đổi tên thành .pdf.</summary>
    public static bool SignatureMatches(string extension, ReadOnlySpan<byte> header)
    {
        return extension.ToLowerInvariant() switch
        {
            ".pdf" => header.StartsWith("%PDF"u8),
            ".png" => header.StartsWith(new byte[] { 0x89, 0x50, 0x4E, 0x47 }),
            ".jpg" or ".jpeg" => header.StartsWith(new byte[] { 0xFF, 0xD8, 0xFF }),
            ".xlsx" or ".docx" => header.StartsWith(new byte[] { 0x50, 0x4B, 0x03, 0x04 }),
            _ => false
        };
    }
}
