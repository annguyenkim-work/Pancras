using Pancras.Domain.Inventory;

namespace Pancras.Application.Inventory;

public interface ITransferAttachmentRepository
{
    Task<IReadOnlyList<TransferAttachment>> ListAsync(Guid transferSlipId, CancellationToken cancellationToken);

    Task<TransferAttachment?> FindAsync(Guid id, CancellationToken cancellationToken);

    Task<IReadOnlyDictionary<Guid, IReadOnlyList<TransferAttachment>>> ListForSlipsAsync(IReadOnlyCollection<Guid> transferSlipIds, CancellationToken cancellationToken);

    Task AddAsync(TransferAttachment attachment, CancellationToken cancellationToken);
}
