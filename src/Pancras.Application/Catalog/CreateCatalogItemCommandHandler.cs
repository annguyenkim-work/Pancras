using FluentValidation;
using Microsoft.Extensions.Logging;
using Pancras.Application.Common;
using Pancras.Domain.Catalog;

namespace Pancras.Application.Catalog;

public sealed class CreateCatalogItemCommandValidator : AbstractValidator<CreateCatalogItemCommand>
{
    public CreateCatalogItemCommandValidator()
    {
        RuleFor(command => command.Code).Must(CatalogValidation.HasText)
            .WithState(_ => new CatalogIssue(CatalogField.Code, CatalogIssueKind.Missing));
        RuleFor(command => command.Name).Must(CatalogValidation.HasText)
            .WithState(_ => new CatalogIssue(CatalogField.Name, CatalogIssueKind.Missing));
        RuleFor(command => command.Category).NotNull()
            .WithState(_ => new CatalogIssue(CatalogField.Category, CatalogIssueKind.Missing));
        RuleFor(command => command.Category).Must(CatalogValidation.KnownCategory)
            .WithState(_ => new CatalogIssue(CatalogField.Category, CatalogIssueKind.Missing));

        RuleFor(command => command.Code).Must(CatalogValidation.FitsCode)
            .WithState(_ => new CatalogIssue(CatalogField.Code, CatalogIssueKind.TooLong, CatalogItem.CodeMaxLength));
        RuleFor(command => command.Name).Must(value => CatalogValidation.Fits(value, CatalogItem.NameMaxLength))
            .WithState(_ => new CatalogIssue(CatalogField.Name, CatalogIssueKind.TooLong, CatalogItem.NameMaxLength));
        RuleFor(command => command.Brand).Must(value => CatalogValidation.Fits(value, CatalogItem.BrandMaxLength))
            .WithState(_ => new CatalogIssue(CatalogField.Brand, CatalogIssueKind.TooLong, CatalogItem.BrandMaxLength));
        RuleFor(command => command.Origin).Must(value => CatalogValidation.Fits(value, CatalogItem.OriginMaxLength))
            .WithState(_ => new CatalogIssue(CatalogField.Origin, CatalogIssueKind.TooLong, CatalogItem.OriginMaxLength));
        RuleFor(command => command.Unit).Must(value => CatalogValidation.Fits(value, CatalogItem.UnitMaxLength))
            .WithState(_ => new CatalogIssue(CatalogField.Unit, CatalogIssueKind.TooLong, CatalogItem.UnitMaxLength));

        RuleFor(command => command.WeightKg).Must(CatalogValidation.NotNegative)
            .WithState(_ => new CatalogIssue(CatalogField.Weight, CatalogIssueKind.Negative));
        RuleFor(command => command.VolumeM3).Must(CatalogValidation.NotNegative)
            .WithState(_ => new CatalogIssue(CatalogField.Volume, CatalogIssueKind.Negative));
        RuleFor(command => command.WarrantyMonths).Must(CatalogValidation.NotNegative)
            .WithState(_ => new CatalogIssue(CatalogField.WarrantyMonths, CatalogIssueKind.Negative));
        RuleFor(command => command.MinimumStock).Must(CatalogValidation.NotNegative)
            .WithState(_ => new CatalogIssue(CatalogField.MinimumStock, CatalogIssueKind.Negative));

        Price(command => command.CostPrice, CatalogField.CostPrice);
        Price(command => command.RetailPrice, CatalogField.RetailPrice);
        Price(command => command.DealerPrice, CatalogField.DealerPrice);
        Price(command => command.DistributorPrice, CatalogField.DistributorPrice);
        Price(command => command.ProjectPrice, CatalogField.ProjectPrice);
    }

    private void Price(
        System.Linq.Expressions.Expression<Func<CreateCatalogItemCommand, decimal?>> property,
        CatalogField field)
    {
        RuleFor(property).NotNull().WithState(_ => new CatalogIssue(field, CatalogIssueKind.Missing));
        RuleFor(property).Must(CatalogValidation.NotNegative)
            .WithState(_ => new CatalogIssue(field, CatalogIssueKind.Negative));
    }
}

public sealed class CreateCatalogItemCommandHandler(
    ICatalogItemRepository repository,
    ILogger<CreateCatalogItemCommandHandler> logger)
{
    private readonly CreateCatalogItemCommandValidator _validator = new();

    public async Task<Result<CreatedCatalogItem>> HandleAsync(
        CreateCatalogItemCommand command,
        CancellationToken cancellationToken)
    {
        var validation = _validator.Validate(command);
        if (!validation.IsValid)
        {
            var issues = CatalogValidation.IssuesFrom(validation);
            logger.LogInformation("Rejected a catalog item with {IssueCount} problems.", issues.Count);
            return Result<CreatedCatalogItem>.Fail(CatalogValidation.Primary(issues), issues);
        }

        var code = command.Code!.Trim();
        var normalizedCode = CatalogItem.NormalizeCode(code);
        if (await repository.NormalizedCodeExistsAsync(normalizedCode, cancellationToken))
        {
            logger.LogWarning("Rejected a duplicate catalog item.");
            return Result<CreatedCatalogItem>.Fail(
                CatalogError.DuplicateCode,
                [new CatalogIssue(CatalogField.Code, CatalogIssueKind.Duplicate)]);
        }

        var item = CatalogItem.Create(
            code,
            command.Name!.Trim(),
            command.Category!.Value,
            command.Brand,
            command.Origin,
            command.Unit,
            command.WeightKg,
            command.VolumeM3,
            command.WarrantyMonths,
            command.MinimumStock,
            command.CostPrice!.Value,
            command.RetailPrice!.Value,
            command.DealerPrice!.Value,
            command.DistributorPrice!.Value,
            command.ProjectPrice!.Value);
        if (!await repository.TryAddAsync(item, cancellationToken))
        {
            logger.LogWarning("Rejected a duplicate catalog item.");
            return Result<CreatedCatalogItem>.Fail(
                CatalogError.DuplicateCode,
                [new CatalogIssue(CatalogField.Code, CatalogIssueKind.Duplicate)]);
        }

        logger.LogInformation("Created catalog item {CatalogItemId}.", item.Id);
        return Result<CreatedCatalogItem>.Ok(new CreatedCatalogItem(item.Id, item.Code, item.Name, item.Category));
    }
}
