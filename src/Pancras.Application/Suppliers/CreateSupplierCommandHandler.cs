using System.Linq.Expressions;
using FluentValidation;
using Microsoft.Extensions.Logging;
using Pancras.Application.Auditing;
using Pancras.Application.Common;
using Pancras.Domain.Auditing;
using Pancras.Domain.Employees;
using Pancras.Domain.Suppliers;

namespace Pancras.Application.Suppliers;

public sealed class CreateSupplierCommandValidator : AbstractValidator<CreateSupplierCommand>
{
    public CreateSupplierCommandValidator()
    {
        Required(command => command.Code, SupplierField.Code);
        Required(command => command.Name, SupplierField.Name);
        Required(command => command.Country, SupplierField.Country);
        Required(command => command.ContactName, SupplierField.ContactName);

        RuleFor(command => command.Code).Must(FitsCode).WithErrorCode(nameof(SupplierError.TooLong))
            .WithState(_ => new SupplierIssue(SupplierField.Code, SupplierIssueKind.TooLong, Supplier.CodeMaxLength));
        Length(command => command.Name, SupplierField.Name, Supplier.NameMaxLength);
        Length(command => command.Country, SupplierField.Country, Supplier.CountryMaxLength);
        Length(command => command.ContactName, SupplierField.ContactName, Supplier.ContactNameMaxLength);
        Length(command => command.Phone, SupplierField.Phone, Supplier.PhoneMaxLength);
        Length(command => command.Email, SupplierField.Email, Supplier.EmailMaxLength);
        Length(command => command.BankName, SupplierField.BankName, Supplier.BankNameMaxLength);
        Length(command => command.Account, SupplierField.Account, Supplier.AccountMaxLength);
        Length(command => command.SwiftCode, SupplierField.SwiftCode, Supplier.SwiftMaxLength);

        // Email, điện thoại và SWIFT là tùy chọn, nhưng nếu nhập thì phải đúng dạng.
        RuleFor(command => command.Email)
            .Must(value => string.IsNullOrWhiteSpace(value) || SupplierEmail.IsAddress(value))
            .WithErrorCode(nameof(SupplierError.InvalidEmail))
            .WithState(_ => new SupplierIssue(SupplierField.Email, SupplierIssueKind.InvalidEmail));

        RuleFor(command => command.Phone)
            .Must(value => string.IsNullOrWhiteSpace(value) || SupplierPhone.IsNumber(value))
            .WithErrorCode(nameof(SupplierError.InvalidPhone))
            .WithState(_ => new SupplierIssue(SupplierField.Phone, SupplierIssueKind.InvalidPhone));

        RuleFor(command => command.SwiftCode)
            .Must(value => string.IsNullOrWhiteSpace(value) || value.Trim().Length > Supplier.SwiftMaxLength || SupplierSwift.IsCode(value))
            .WithErrorCode(nameof(SupplierError.InvalidSwift))
            .WithState(_ => new SupplierIssue(SupplierField.SwiftCode, SupplierIssueKind.InvalidSwift));
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

        if (issues.Any(issue => issue.Kind == SupplierIssueKind.InvalidSwift))
        {
            return SupplierError.InvalidSwift;
        }

        return SupplierError.InvalidPhone;
    }

    private void Required(Expression<Func<CreateSupplierCommand, string?>> property, SupplierField field) =>
        RuleFor(property).Must(HasText).WithErrorCode(nameof(SupplierError.Incomplete))
            .WithState(_ => new SupplierIssue(field, SupplierIssueKind.Missing));

    private void Length(Expression<Func<CreateSupplierCommand, string?>> property, SupplierField field, int max) =>
        RuleFor(property).Must(value => Fits(value, max)).WithErrorCode(nameof(SupplierError.TooLong))
            .WithState(_ => new SupplierIssue(field, SupplierIssueKind.TooLong, max));

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
    IUnitOfWork unitOfWork,
    ILogger<CreateSupplierCommandHandler> logger,
    TimeProvider? clock = null,
    IRequestInfo? request = null,
    IAuditLog? audit = null)
{
    private readonly CreateSupplierCommandValidator _validator = new();

    public async Task<Result<CreatedSupplier, SupplierError>> HandleAsync(
        CreateSupplierCommand command,
        CancellationToken cancellationToken)
    {
        if (!SupplierAccess.CanManage(command.Actor.Role))
        {
            logger.LogWarning("Supplier creation forbidden for role {Role}.", command.Actor.Role);
            return Result<CreatedSupplier, SupplierError>.Fail(SupplierError.Forbidden);
        }

        var validation = _validator.Validate(command);
        if (!validation.IsValid)
        {
            var issues = CreateSupplierCommandValidator.IssuesFrom(validation);
            logger.LogInformation("Rejected a supplier profile with {IssueCount} problems.", issues.Count);
            return Result<CreatedSupplier, SupplierError>.Fail(CreateSupplierCommandValidator.Primary(issues), issues);
        }

        var code = command.Code!.Trim();
        var normalizedCode = Supplier.NormalizeCode(code);
        if (await repository.NormalizedCodeExistsAsync(normalizedCode, null, cancellationToken))
        {
            logger.LogInformation("Rejected a duplicate supplier code.");
            return Duplicate(SupplierIssueKind.Duplicate);
        }

        if (await repository.InactiveNameWithCodeAsync(normalizedCode, cancellationToken) is not null)
        {
            logger.LogInformation("Rejected a supplier code held by an inactive supplier.");
            return Duplicate(SupplierIssueKind.DuplicateInactive);
        }

        await using var scope = await unitOfWork.BeginAsync(cancellationToken);
        var supplier = Supplier.Create(
            code,
            command.Name!.Trim(),
            command.Country!.Trim(),
            command.ContactName!.Trim(),
            command.Phone ?? string.Empty,
            command.Email ?? string.Empty,
            command.Account ?? string.Empty,
            command.BankName ?? string.Empty,
            command.SwiftCode ?? string.Empty);
        await repository.AddAsync(supplier, cancellationToken);
        audit.Stage(clock ?? TimeProvider.System, request, command.Actor.Id, null, AuditActions.SupplierCreated, "supplier", supplier.Id.ToString("D"), supplier.Code);
        if ((await scope.CommitAsync(cancellationToken)).ThrowIfFailed() != SaveOutcome.Saved)
        {
            logger.LogInformation("Rejected a duplicate supplier code.");
            return Duplicate(SupplierIssueKind.Duplicate);
        }

        logger.LogInformation("Created supplier {SupplierId}.", supplier.Id);
        return Result<CreatedSupplier, SupplierError>.Ok(new CreatedSupplier(supplier.Id, supplier.Code, supplier.Name));
    }

    private static Result<CreatedSupplier, SupplierError> Duplicate(SupplierIssueKind kind) =>
        Result<CreatedSupplier, SupplierError>.Fail(SupplierError.DuplicateCode, [new SupplierIssue(SupplierField.Code, kind)]);
}
