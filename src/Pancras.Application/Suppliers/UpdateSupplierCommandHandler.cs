using Microsoft.Extensions.Logging;
using Pancras.Application.Common;
using Pancras.Domain.Suppliers;

namespace Pancras.Application.Suppliers;

public sealed class UpdateSupplierCommandHandler(
    ISupplierRepository repository,
    ILogger<UpdateSupplierCommandHandler> logger)
{
    private readonly CreateSupplierCommandValidator _validator = new();

    public async Task<Result<SupplierProfile>> HandleAsync(
        UpdateSupplierCommand command,
        CancellationToken cancellationToken)
    {
        var validation = _validator.Validate(new CreateSupplierCommand
        {
            Code = command.Code,
            Name = command.Name,
            Country = command.Country,
            ContactName = command.ContactName,
            Phone = command.Phone,
            Email = command.Email,
            Account = command.Account
        });
        if (!validation.IsValid)
        {
            var issues = CreateSupplierCommandValidator.IssuesFrom(validation);
            logger.LogInformation("Rejected a supplier update with {IssueCount} problems.", issues.Count);
            return Result<SupplierProfile>.Fail(CreateSupplierCommandValidator.Primary(issues), issues);
        }

        var supplier = await repository.GetActiveForUpdateAsync(command.Id, cancellationToken);
        if (supplier is null)
        {
            logger.LogInformation("Supplier profile was not found.");
            return Result<SupplierProfile>.Fail(SupplierError.NotFound);
        }

        var code = command.Code!.Trim();
        var normalizedCode = Supplier.NormalizeCode(code);
        if (await repository.NormalizedCodeExistsAsync(normalizedCode, supplier.Id, cancellationToken))
        {
            logger.LogWarning("Rejected a duplicate supplier code.");
            return Result<SupplierProfile>.Fail(
                SupplierError.DuplicateCode,
                [new SupplierIssue(SupplierField.Code, SupplierIssueKind.Duplicate)]);
        }

        supplier.Update(
            code,
            command.Name!.Trim(),
            command.Country!.Trim(),
            command.ContactName!.Trim(),
            command.Phone!.Trim(),
            command.Email!.Trim(),
            command.Account!.Trim());
        if (!await repository.TrySaveAsync(cancellationToken))
        {
            logger.LogWarning("Rejected a duplicate supplier code.");
            return Result<SupplierProfile>.Fail(
                SupplierError.DuplicateCode,
                [new SupplierIssue(SupplierField.Code, SupplierIssueKind.Duplicate)]);
        }

        logger.LogInformation("Updated supplier {SupplierId}.", supplier.Id);
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
