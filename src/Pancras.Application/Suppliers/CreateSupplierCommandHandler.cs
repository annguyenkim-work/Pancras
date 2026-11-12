using FluentValidation;
using Microsoft.Extensions.Logging;
using Pancras.Application.Common;
using Pancras.Domain.Suppliers;

namespace Pancras.Application.Suppliers;

public sealed class CreateSupplierCommandValidator : AbstractValidator<CreateSupplierCommand>
{
    public CreateSupplierCommandValidator()
    {
        RuleFor(command => command.Code).Must(HasText).WithErrorCode(nameof(SupplierError.Incomplete))
            .WithState(_ => new SupplierIssue(SupplierField.Code, SupplierIssueKind.Missing));
        RuleFor(command => command.Name).Must(HasText).WithErrorCode(nameof(SupplierError.Incomplete))
            .WithState(_ => new SupplierIssue(SupplierField.Name, SupplierIssueKind.Missing));
        RuleFor(command => command.Country).Must(HasText).WithErrorCode(nameof(SupplierError.Incomplete))
            .WithState(_ => new SupplierIssue(SupplierField.Country, SupplierIssueKind.Missing));
        RuleFor(command => command.ContactName).Must(HasText).WithErrorCode(nameof(SupplierError.Incomplete))
            .WithState(_ => new SupplierIssue(SupplierField.ContactName, SupplierIssueKind.Missing));
        RuleFor(command => command.Phone).Must(HasText).WithErrorCode(nameof(SupplierError.Incomplete))
            .WithState(_ => new SupplierIssue(SupplierField.Phone, SupplierIssueKind.Missing));
        RuleFor(command => command.Email).Must(HasText).WithErrorCode(nameof(SupplierError.Incomplete))
            .WithState(_ => new SupplierIssue(SupplierField.Email, SupplierIssueKind.Missing));
        RuleFor(command => command.Account).Must(HasText).WithErrorCode(nameof(SupplierError.Incomplete))
            .WithState(_ => new SupplierIssue(SupplierField.Account, SupplierIssueKind.Missing));

        RuleFor(command => command.Code).Must(FitsCode).WithErrorCode(nameof(SupplierError.TooLong))
            .WithState(_ => new SupplierIssue(SupplierField.Code, SupplierIssueKind.TooLong, Supplier.CodeMaxLength));
        RuleFor(command => command.Name).Must(value => Fits(value, Supplier.NameMaxLength)).WithErrorCode(nameof(SupplierError.TooLong))
            .WithState(_ => new SupplierIssue(SupplierField.Name, SupplierIssueKind.TooLong, Supplier.NameMaxLength));
        RuleFor(command => command.Country).Must(value => Fits(value, Supplier.CountryMaxLength)).WithErrorCode(nameof(SupplierError.TooLong))
            .WithState(_ => new SupplierIssue(SupplierField.Country, SupplierIssueKind.TooLong, Supplier.CountryMaxLength));
        RuleFor(command => command.ContactName).Must(value => Fits(value, Supplier.ContactNameMaxLength)).WithErrorCode(nameof(SupplierError.TooLong))
            .WithState(_ => new SupplierIssue(SupplierField.ContactName, SupplierIssueKind.TooLong, Supplier.ContactNameMaxLength));
        RuleFor(command => command.Phone).Must(value => Fits(value, Supplier.PhoneMaxLength)).WithErrorCode(nameof(SupplierError.TooLong))
            .WithState(_ => new SupplierIssue(SupplierField.Phone, SupplierIssueKind.TooLong, Supplier.PhoneMaxLength));
        RuleFor(command => command.Email).Must(value => Fits(value, Supplier.EmailMaxLength)).WithErrorCode(nameof(SupplierError.TooLong))
            .WithState(_ => new SupplierIssue(SupplierField.Email, SupplierIssueKind.TooLong, Supplier.EmailMaxLength));
        RuleFor(command => command.Account).Must(value => Fits(value, Supplier.AccountMaxLength)).WithErrorCode(nameof(SupplierError.TooLong))
            .WithState(_ => new SupplierIssue(SupplierField.Account, SupplierIssueKind.TooLong, Supplier.AccountMaxLength));

        RuleFor(command => command.Email)
            .Must(value => string.IsNullOrWhiteSpace(value) || SupplierEmail.IsAddress(value))
            .WithErrorCode(nameof(SupplierError.InvalidEmail))
            .WithState(_ => new SupplierIssue(SupplierField.Email, SupplierIssueKind.InvalidEmail));

        RuleFor(command => command.Phone)
            .Must(value => string.IsNullOrWhiteSpace(value) || SupplierPhone.IsNumber(value))
            .WithErrorCode(nameof(SupplierError.InvalidPhone))
            .WithState(_ => new SupplierIssue(SupplierField.Phone, SupplierIssueKind.InvalidPhone));
    }

    internal static IReadOnlyList<SupplierIssue> IssuesFrom(FluentValidation.Results.ValidationResult validation) =>
        validation.Errors
            .Select(error => error.CustomState)
            .OfType<SupplierIssue>()
            .Distinct()
            .ToArray();

    internal static SupplierError Primary(IReadOnlyList<SupplierIssue> issues)
    {
        if (issues.Any(issue => issue.Kind == SupplierIssueKind.Missing))
        {
            return SupplierError.Incomplete;
        }

        if (issues.Any(issue => issue.Kind == SupplierIssueKind.TooLong))
        {
            return SupplierError.TooLong;
        }

        if (issues.Any(issue => issue.Kind == SupplierIssueKind.InvalidEmail))
        {
            return SupplierError.InvalidEmail;
        }

        return SupplierError.InvalidPhone;
    }

    private static bool HasText(string? value) => !string.IsNullOrWhiteSpace(value);

    private static bool Fits(string? value, int maxLength) =>
        string.IsNullOrWhiteSpace(value) || value.Trim().Length <= maxLength;

    private static bool FitsCode(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        var trimmed = value.Trim();
        return trimmed.Length <= Supplier.CodeMaxLength
            && Supplier.NormalizeCode(trimmed).Length <= Supplier.CodeMaxLength;
    }
}

public sealed class CreateSupplierCommandHandler(
    ISupplierRepository repository,
    ILogger<CreateSupplierCommandHandler> logger)
{
    private readonly CreateSupplierCommandValidator _validator = new();

    public async Task<Result<CreatedSupplier>> HandleAsync(
        CreateSupplierCommand command,
        CancellationToken cancellationToken)
    {
        var validation = _validator.Validate(command);
        if (!validation.IsValid)
        {
            var issues = CreateSupplierCommandValidator.IssuesFrom(validation);
            logger.LogInformation("Rejected a supplier profile with {IssueCount} problems.", issues.Count);
            return Result<CreatedSupplier>.Fail(CreateSupplierCommandValidator.Primary(issues), issues);
        }

        var code = command.Code!.Trim();
        var normalizedCode = Supplier.NormalizeCode(code);
        if (await repository.NormalizedCodeExistsAsync(normalizedCode, null, cancellationToken))
        {
            logger.LogWarning("Rejected a duplicate supplier code.");
            return Result<CreatedSupplier>.Fail(
                SupplierError.DuplicateCode,
                [new SupplierIssue(SupplierField.Code, SupplierIssueKind.Duplicate)]);
        }

        var supplier = Supplier.Create(
            code,
            command.Name!.Trim(),
            command.Country!.Trim(),
            command.ContactName!.Trim(),
            command.Phone!.Trim(),
            command.Email!.Trim(),
            command.Account!.Trim());
        if (!await repository.TryAddAsync(supplier, cancellationToken))
        {
            logger.LogWarning("Rejected a duplicate supplier code.");
            return Result<CreatedSupplier>.Fail(
                SupplierError.DuplicateCode,
                [new SupplierIssue(SupplierField.Code, SupplierIssueKind.Duplicate)]);
        }

        logger.LogInformation("Created supplier {SupplierId}.", supplier.Id);
        return Result<CreatedSupplier>.Ok(new CreatedSupplier(supplier.Id, supplier.Code, supplier.Name));
    }
}
