using Pancras.Domain.Inventory;

namespace Pancras.Application.Inventory;

public interface IStockDocumentAttachmentRepository
{
    Task<IReadOnlyList<StockDocumentAttachment>> ListAsync(StockDocumentKind kind, Guid documentId, CancellationToken cancellationToken);

    Task<StockDocumentAttachment?> FindAsync(Guid id, CancellationToken cancellationToken);

    Task<IReadOnlyDictionary<Guid, IReadOnlyList<StockDocumentAttachment>>> ListForDocumentsAsync(IReadOnlyCollection<Guid> documentIds, CancellationToken cancellationToken);

    Task AddAsync(StockDocumentAttachment attachment, CancellationToken cancellationToken);
}
