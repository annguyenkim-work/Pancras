namespace Pancras.Application.Purchasing;

public sealed class ListPurchaseAttachmentsHandler(IPurchaseAttachmentRepository attachments)
{
    public async Task<IReadOnlyList<PurchaseAttachmentView>> HandleAsync(Guid purchaseOrderId, CancellationToken cancellationToken) =>
        (await attachments.ListAsync(purchaseOrderId, cancellationToken))
            .OrderBy(item => item.UploadedAt)
            .Select(item => new PurchaseAttachmentView(item.Id, item.Kind, item.FileName, item.SizeBytes, item.UploadedAt, item.UploadedBy))
            .ToArray();
}
