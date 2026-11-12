using Microsoft.Extensions.Logging;
using Pancras.Application.Common;

namespace Pancras.Application.Suppliers;

public sealed class DeleteSupplierCommandHandler(
    ISupplierRepository repository,
    ILogger<DeleteSupplierCommandHandler> logger)
{
    public async Task<Result<Guid>> HandleAsync(Guid id, CancellationToken cancellationToken)
    {
        var supplier = await repository.GetActiveForUpdateAsync(id, cancellationToken);
        if (supplier is null)
        {
            logger.LogInformation("Supplier profile was not found.");
            return Result<Guid>.Fail(SupplierError.NotFound);
        }

        supplier.MarkDeleted();
        if (!await repository.TrySaveAsync(cancellationToken))
        {
            logger.LogWarning("Could not save a supplier deletion for {SupplierId}.", supplier.Id);
            return Result<Guid>.Fail(SupplierError.NotFound);
        }

        logger.LogInformation("Soft-deleted supplier {SupplierId}.", supplier.Id);
        return Result<Guid>.Ok(supplier.Id);
    }
}
