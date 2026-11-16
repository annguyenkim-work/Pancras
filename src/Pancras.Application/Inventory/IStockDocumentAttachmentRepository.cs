using Microsoft.Extensions.Logging;
using Pancras.Application.Auditing;
using Pancras.Application.Common;
using Pancras.Application.Purchasing;
using Pancras.Domain.Auditing;
using Pancras.Domain.Employees;
using Pancras.Domain.Inventory;
using Pancras.Domain.Purchasing;

namespace Pancras.Application.Inventory;

public interface IStockDocumentAttachmentRepository
{
    Task<IReadOnlyList<StockDocumentAttachment>> ListAsync(StockDocumentKind kind, Guid documentId, CancellationToken cancellationToken);

    Task<StockDocumentAttachment?> FindAsync(Guid id, CancellationToken cancellationToken);

    Task<IReadOnlyDictionary<Guid, IReadOnlyList<StockDocumentAttachment>>> ListForDocumentsAsync(IReadOnlyCollection<Guid> documentIds, CancellationToken cancellationToken);

    Task AddAsync(StockDocumentAttachment attachment, CancellationToken cancellationToken);
}
