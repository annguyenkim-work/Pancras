using Microsoft.Extensions.Logging;
using Pancras.Application.Auditing;
using Pancras.Application.Common;
using Pancras.Application.Purchasing;
using Pancras.Domain.Auditing;
using Pancras.Domain.Employees;
using Pancras.Domain.Inventory;
using Pancras.Domain.Purchasing;

namespace Pancras.Application.Inventory;

public interface ITransferAttachmentRepository
{
    Task<IReadOnlyList<TransferAttachment>> ListAsync(Guid transferSlipId, CancellationToken cancellationToken);

    Task<TransferAttachment?> FindAsync(Guid id, CancellationToken cancellationToken);

    Task<IReadOnlyDictionary<Guid, IReadOnlyList<TransferAttachment>>> ListForSlipsAsync(IReadOnlyCollection<Guid> transferSlipIds, CancellationToken cancellationToken);

    Task AddAsync(TransferAttachment attachment, CancellationToken cancellationToken);
}
