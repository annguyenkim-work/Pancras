using Pancras.Domain.Purchasing;

namespace Pancras.Application.Purchasing;

public sealed record PurchaseAttachmentView(Guid Id, AttachmentKind Kind, string FileName, long SizeBytes, DateTimeOffset UploadedAt, string UploadedBy);

public sealed record AttachmentDownload(Stream Content, string FileName, string ContentType);

public sealed class UploadPurchaseAttachmentCommand
{
    public Pancras.Domain.Employees.EmployeeActor Actor { get; set; } = new(Guid.Empty, null, null);

    public Guid PurchaseOrderId { get; set; }

    public AttachmentKind Kind { get; set; }

    public string? FileName { get; set; }

    public Stream? Content { get; set; }
}
