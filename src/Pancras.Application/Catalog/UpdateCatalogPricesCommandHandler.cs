using Pancras.Application.Auditing;
using FluentValidation;
using Microsoft.Extensions.Logging;
using Pancras.Application.Common;
using Pancras.Domain.Catalog;

namespace Pancras.Application.Catalog;

public sealed class UpdateCatalogPricesCommandValidator : AbstractValidator<UpdateCatalogPricesCommand>
{
    public UpdateCatalogPricesCommandValidator()
    {
        Price(command => command.CostPrice, CatalogField.CostPrice);
        Price(command => command.RetailPrice, CatalogField.RetailPrice);
        Price(command => command.DealerPrice, CatalogField.DealerPrice);
        Price(command => command.DistributorPrice, CatalogField.DistributorPrice);
        Price(command => command.ProjectPrice, CatalogField.ProjectPrice);

        When(command => command.IncludeDetails, () =>
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
            RuleFor(command => command.HeightMm).Must(CatalogValidation.NotNegative)
                .WithState(_ => new CatalogIssue(CatalogField.Height, CatalogIssueKind.Negative));
            RuleFor(command => command.LengthMm).Must(CatalogValidation.NotNegative)
                .WithState(_ => new CatalogIssue(CatalogField.Length, CatalogIssueKind.Negative));
            RuleFor(command => command.WidthMm).Must(CatalogValidation.NotNegative)
                .WithState(_ => new CatalogIssue(CatalogField.Width, CatalogIssueKind.Negative));
        });

        When(command => command.ReplaceImage, () =>
        {
            RuleFor(command => command.ImagePath).Must(value => CatalogValidation.Fits(value, CatalogItem.ImagePathMaxLength))
                .WithState(_ => new CatalogIssue(CatalogField.Image, CatalogIssueKind.TooLong, CatalogItem.ImagePathMaxLength));
        });
    }

    private void Price(
        System.Linq.Expressions.Expression<Func<UpdateCatalogPricesCommand, decimal?>> property,
        CatalogField field)
    {
        RuleFor(property).NotNull().WithState(_ => new CatalogIssue(field, CatalogIssueKind.Missing));
        RuleFor(property).Must(CatalogValidation.NotNegative)
            .WithState(_ => new CatalogIssue(field, CatalogIssueKind.Negative));
    }
}

public sealed class UpdateCatalogPricesCommandHandler(
    ICatalogItemRepository repository,
    ILogger<UpdateCatalogPricesCommandHandler> logger,
    TimeProvider? clock = null,
    Pancras.Application.Auditing.IAuditLog? audit = null,
    Pancras.Application.Auditing.IRequestInfo? request = null)
{
    private readonly UpdateCatalogPricesCommandValidator _validator = new();

    public async Task<Result<CatalogItemRow>> HandleAsync(
        UpdateCatalogPricesCommand command,
        CancellationToken cancellationToken)
    {
        if (!PriceListRole.CanEdit(command.Role))
        {
            logger.LogWarning("Rejected a price list edit.");
            return Result<CatalogItemRow>.Fail(CatalogError.PriceListForbidden);
        }

        var item = await repository.GetVisibleForUpdateAsync(command.Id, cancellationToken);
        if (item is null)
        {
            logger.LogWarning("Rejected a price list edit.");
            return Result<CatalogItemRow>.Fail(CatalogError.NotFound);
        }

        var validation = _validator.Validate(command);
        if (!validation.IsValid)
        {
            var issues = CatalogValidation.IssuesFrom(validation);
            logger.LogInformation("Rejected a price list edit for {CatalogItemId} with {IssueCount} problems.", item.Id, issues.Count);
            return Result<CatalogItemRow>.Fail(CatalogValidation.Primary(issues), issues);
        }

        if (command.IncludeDetails)
        {
            var normalized = CatalogItem.NormalizeCode(command.Code!.Trim());
            if (!string.Equals(normalized, item.NormalizedCode, StringComparison.Ordinal)
                && await repository.NormalizedCodeExistsAsync(normalized, cancellationToken))
            {
                logger.LogWarning("Rejected a duplicate catalog item.");
                return Result<CatalogItemRow>.Fail(
                    CatalogError.DuplicateCode,
                    [new CatalogIssue(CatalogField.Code, CatalogIssueKind.Duplicate)]);
            }

            item.UpdateDetails(
                command.Code!.Trim(),
                command.Name!.Trim(),
                command.Category!.Value,
                command.Brand,
                command.Origin,
                command.Unit,
                command.WeightKg,
                command.VolumeM3,
                command.WarrantyMonths,
                command.MinimumStock,
                command.HeightMm,
                command.LengthMm,
                command.WidthMm);
        }

        if (command.ReplaceImage)
        {
            item.SetImage(command.ImagePath);
        }

        item.UpdatePrices(
            command.CostPrice!.Value,
            command.RetailPrice!.Value,
            command.DealerPrice!.Value,
            command.DistributorPrice!.Value,
            command.ProjectPrice!.Value);
        if (!await repository.TrySaveAsync(cancellationToken))
        {
            logger.LogWarning("Rejected a price list edit for {CatalogItemId}.", item.Id);
            return Result<CatalogItemRow>.Fail(CatalogError.NotFound);
        }

        logger.LogInformation("Updated prices for catalog item {CatalogItemId}.", item.Id);
        await audit.RecordAsync(clock ?? TimeProvider.System, request, null, null, Pancras.Domain.Auditing.AuditActions.PricesChanged, "catalog_item", item.Id.ToString("D"), null, cancellationToken);
        return Result<CatalogItemRow>.Ok(CatalogItemRow.From(item));
    }
}
