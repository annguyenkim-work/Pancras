using Microsoft.Extensions.Logging;
using Pancras.Application.Common;
using Pancras.Domain.Suppliers;

namespace Pancras.Application.Suppliers;

public sealed class GetSupplierProfileHandler(
    ISupplierRepository repository,
    ILogger<GetSupplierProfileHandler> logger)
{
    public async Task<Result<SupplierProfile>> HandleAsync(Guid id, CancellationToken cancellationToken)
    {
        var supplier = await repository.GetActiveReadOnlyAsync(id, cancellationToken);
        if (supplier is null)
        {
            logger.LogInformation("Supplier profile was not found.");
            return Result<SupplierProfile>.Fail(SupplierError.NotFound);
        }

        return Result<SupplierProfile>.Ok(new SupplierProfile(
            supplier.Id,
            supplier.Code,
            supplier.Name,
            supplier.Country,
            supplier.ContactName,
            supplier.Phone,
            supplier.Email,
            supplier.Account));
    }
}
