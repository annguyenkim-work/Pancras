using Microsoft.Extensions.Logging;
using Pancras.Application.Auditing;
using Pancras.Application.Common;
using Pancras.Domain.Auditing;
using Pancras.Domain.Employees;
using Pancras.Domain.Suppliers;

namespace Pancras.Application.Suppliers;

public sealed class UpdateSupplierCommandHandler(
    ISupplierRepository repository,
    ILogger<UpdateSupplierCommandHandler> logger,
    TimeProvider? clock = null,
    IRequestInfo? request = null,
    IAuditLog? audit = null)
{
    private readonly CreateSupplierCommandValidator _validator = new();

    public async Task<Result<SupplierProfile, SupplierError>> HandleAsync(
        UpdateSupplierCommand command,
        CancellationToken cancellationToken)
    {
        if (!SupplierAccess.CanManage(command.Actor.Role))
        {
            logger.LogWarning("Supplier update forbidden for role {Role}.", command.Actor.Role);
            return Result<SupplierProfile, SupplierError>.Fail(SupplierError.Forbidden);
        }

        var validation = _validator.Validate(new CreateSupplierCommand
        {
            Code = command.Code,
            Name = command.Name,
            Country = command.Country,
            ContactName = command.ContactName,
            Phone = command.Phone,
            Email = command.Email,
            BankName = command.BankName,
            Account = command.Account,
            SwiftCode = command.SwiftCode
        });
        if (!validation.IsValid)
        {
            var issues = CreateSupplierCommandValidator.IssuesFrom(validation);
            logger.LogInformation("Rejected a supplier update with {IssueCount} problems.", issues.Count);
            return Result<SupplierProfile, SupplierError>.Fail(CreateSupplierCommandValidator.Primary(issues), issues);
        }

        var supplier = await repository.GetActiveForUpdateAsync(command.Id, cancellationToken);
        if (supplier is null)
        {
            logger.LogInformation("Supplier profile was not found.");
            return Result<SupplierProfile, SupplierError>.Fail(SupplierError.NotFound);
        }

        if (command.Version != supplier.Version)
        {
            logger.LogInformation("Rejected a stale supplier edit for {SupplierId}.", supplier.Id);
            return Result<SupplierProfile, SupplierError>.Fail(SupplierError.VersionConflict);
        }

        var code = command.Code!.Trim();
        var normalizedCode = Supplier.NormalizeCode(code);
        if (await repository.NormalizedCodeExistsAsync(normalizedCode, supplier.Id, cancellationToken))
        {
            logger.LogWarning("Rejected a duplicate supplier code.");
            return DuplicateCode(SupplierIssueKind.Duplicate);
        }

        if (normalizedCode != supplier.NormalizedCode
            && await repository.InactiveNameWithCodeAsync(normalizedCode, cancellationToken) is not null)
        {
            logger.LogWarning("Rejected a supplier code held by an inactive supplier.");
            return DuplicateCode(SupplierIssueKind.DuplicateInactive);
        }

        var name = command.Name!.Trim();
        var country = command.Country!.Trim();
        var contact = command.ContactName!.Trim();
        var phone = command.Phone?.Trim() ?? string.Empty;
        var email = command.Email?.Trim() ?? string.Empty;
        var bank = command.BankName?.Trim() ?? string.Empty;
        var account = command.Account?.Trim() ?? string.Empty;
        var swift = (command.SwiftCode?.Trim() ?? string.Empty).ToUpperInvariant();

        var changes = new List<string>();
        Track(changes, "mã", supplier.Code, code, mask: false);
        Track(changes, "tên", supplier.Name, name, mask: false);
        Track(changes, "quốc gia", supplier.Country, country, mask: false);
        Track(changes, "người liên hệ", supplier.ContactName, contact, mask: false);
        Track(changes, "điện thoại", supplier.Phone, phone, mask: false);
        Track(changes, "email", supplier.Email, email, mask: false);
        Track(changes, "ngân hàng", supplier.BankName, bank, mask: false);
        Track(changes, "tài khoản", supplier.Account, account, mask: true);
        Track(changes, "SWIFT", supplier.SwiftCode, swift, mask: false);

        supplier.Update(code, name, country, contact, phone, email, account, bank, swift);
        var saved = await repository.TrySaveAsync(cancellationToken);
        if (saved == SaveOutcome.Conflict)
        {
            logger.LogInformation("Rejected a concurrent supplier edit.");
            return Result<SupplierProfile, SupplierError>.Fail(SupplierError.VersionConflict);
        }

        if (saved != SaveOutcome.Saved)
        {
            logger.LogWarning("Rejected a duplicate supplier code.");
            return DuplicateCode(SupplierIssueKind.Duplicate);
        }

        logger.LogInformation("Updated supplier {SupplierId}.", supplier.Id);
        await audit.RecordAsync(
            clock ?? TimeProvider.System,
            request,
            command.Actor.Id,
            null,
            AuditActions.SupplierUpdated,
            "supplier",
            supplier.Id.ToString("D"),
            changes.Count == 0 ? "không đổi" : string.Join("; ", changes),
            cancellationToken);
        return Result<SupplierProfile, SupplierError>.Ok(new SupplierProfile(
            supplier.Id,
            supplier.Code,
            supplier.Name,
            supplier.Country,
            supplier.ContactName,
            supplier.Phone,
            supplier.Email,
            supplier.BankName,
            supplier.Account,
            supplier.SwiftCode,
            Version: supplier.Version));
    }

    private static Result<SupplierProfile, SupplierError> DuplicateCode(SupplierIssueKind kind) =>
        Result<SupplierProfile, SupplierError>.Fail(SupplierError.DuplicateCode, [new SupplierIssue(SupplierField.Code, kind)]);

    /// <summary>Ghi trường nào đổi từ gì sang gì. Số tài khoản chỉ ghi dạng che để nhật ký không lộ số đầy đủ.</summary>
    private static void Track(List<string> changes, string label, string before, string after, bool mask)
    {
        if (string.Equals(before, after, StringComparison.Ordinal))
        {
            return;
        }

        changes.Add(mask
            ? $"{label}: {SupplierBankMask.Of(before)} → {SupplierBankMask.Of(after)}"
            : $"{label}: \"{before}\" → \"{after}\"");
    }
}
