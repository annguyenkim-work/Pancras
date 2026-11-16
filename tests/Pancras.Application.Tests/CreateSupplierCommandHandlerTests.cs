using Microsoft.Extensions.Logging.Abstractions;
using Pancras.Application.Common;
using Pancras.Application.Suppliers;
using Pancras.Domain.Suppliers;

namespace Pancras.Application.Tests;

public class CreateSupplierCommandHandlerTests
{
    private readonly FakeSupplierRepository _repository = new();

    [Fact]
    public async Task MissingField_DoesNotInsert()
    {
        var result = await Handler().HandleAsync(Valid(name: " "), CancellationToken.None);

        Assert.Equal(SupplierError.Incomplete, result.Error);
        Assert.Contains(result.IssuesOf<SupplierIssue>(), issue => issue.Field == SupplierField.Name && issue.Kind == SupplierIssueKind.Missing);
        Assert.Empty(_repository.Suppliers);
    }

    [Fact]
    public async Task SeveralProblems_AreAllReported()
    {
        var result = await Handler().HandleAsync(Valid(name: " ", email: "khong-phai-email"), CancellationToken.None);

        Assert.Equal(SupplierError.Incomplete, result.Error);
        Assert.Contains(result.IssuesOf<SupplierIssue>(), issue => issue.Field == SupplierField.Name && issue.Kind == SupplierIssueKind.Missing);
        Assert.Contains(result.IssuesOf<SupplierIssue>(), issue => issue.Field == SupplierField.Email && issue.Kind == SupplierIssueKind.InvalidEmail);
        Assert.Empty(_repository.Suppliers);
    }

    [Fact]
    public async Task InvalidEmail_DoesNotInsert()
    {
        var result = await Handler().HandleAsync(Valid(email: "khong-phai-email"), CancellationToken.None);

        Assert.Equal(SupplierError.InvalidEmail, result.Error);
        Assert.Empty(_repository.Suppliers);
    }

    [Fact]
    public async Task InvalidPhone_DoesNotInsert()
    {
        var letters = await Handler().HandleAsync(Valid(phone: "abc"), CancellationToken.None);
        var tooShort = await Handler().HandleAsync(Valid(phone: "0901"), CancellationToken.None);
        var international = await Handler().HandleAsync(Valid(phone: "+86 138 0000 0000"), CancellationToken.None);

        Assert.Equal(SupplierError.InvalidPhone, letters.Error);
        Assert.Contains(letters.IssuesOf<SupplierIssue>(), issue => issue.Field == SupplierField.Phone && issue.Kind == SupplierIssueKind.InvalidPhone);
        Assert.Equal(SupplierError.InvalidPhone, tooShort.Error);
        Assert.True(international.IsSuccess);
        Assert.Single(_repository.Suppliers);
    }

    [Fact]
    public async Task CodeThatGrowsWhenUppercased_IsTooLong()
    {
        char? growing = null;
        for (var value = 0x00A0; value <= 0xFFFD; value++)
        {
            if (char.IsSurrogate((char)value))
            {
                continue;
            }

            var single = char.ToString((char)value);
            if (single.Trim().Length == 0)
            {
                continue;
            }

            if (Supplier.NormalizeCode(single).Length > 1)
            {
                growing = (char)value;
                break;
            }
        }

        if (growing is not char mark)
        {
            Assert.Equal(Supplier.CodeMaxLength, Supplier.NormalizeCode(new string('a', Supplier.CodeMaxLength)).Length);
            return;
        }

        var code = new string('a', Supplier.CodeMaxLength - 1) + mark;
        var result = await Handler().HandleAsync(Valid(code: code), CancellationToken.None);

        Assert.Equal(SupplierError.TooLong, result.Error);
        Assert.Empty(_repository.Suppliers);
    }

    [Fact]
    public async Task TooLongCode_DoesNotInsert()
    {
        var result = await Handler().HandleAsync(Valid(code: new string('A', 33)), CancellationToken.None);

        Assert.Equal(SupplierError.TooLong, result.Error);
        Assert.Empty(_repository.Suppliers);
    }

    [Fact]
    public async Task DuplicateCode_DoesNotInsertAgain()
    {
        var handler = Handler();
        var first = await handler.HandleAsync(Valid(), CancellationToken.None);
        var second = await handler.HandleAsync(Valid(code: "ncc01", name: "Nhà khác"), CancellationToken.None);

        Assert.True(first.IsSuccess);
        Assert.Equal(SupplierError.DuplicateCode, second.Error);
        Assert.Contains(second.IssuesOf<SupplierIssue>(), issue => issue.Field == SupplierField.Code && issue.Kind == SupplierIssueKind.Duplicate);
        Assert.Single(_repository.Suppliers);
    }

    [Fact]
    public async Task ValidProfile_InsertsOnce()
    {
        var result = await Handler().HandleAsync(Valid(code: " ncc01 "), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var supplier = Assert.Single(_repository.Suppliers);
        Assert.Equal("ncc01", supplier.Code);
        Assert.Equal("NCC01", supplier.NormalizedCode);
        Assert.Equal(result.Value!.Id, supplier.Id);
    }

    [Fact]
    public async Task OnlyManagersCanCreate()
    {
        var keeper = await Handler().HandleAsync(Valid(actor: SupplierActors.Keeper), CancellationToken.None);
        var sales = await Handler().HandleAsync(Valid(actor: SupplierActors.Sales), CancellationToken.None);

        Assert.Equal(SupplierError.Forbidden, keeper.Error);
        Assert.Equal(SupplierError.Forbidden, sales.Error);
        Assert.Empty(_repository.Suppliers);
    }

    [Fact]
    public async Task EmailPhoneAndPaymentDetails_AreOptional()
    {
        var command = Valid();
        command.Email = null;
        command.Phone = " ";
        command.Account = null;

        var result = await Handler().HandleAsync(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        var supplier = Assert.Single(_repository.Suppliers);
        Assert.Equal(string.Empty, supplier.Email);
        Assert.Equal(string.Empty, supplier.Phone);
        Assert.Equal(string.Empty, supplier.Account);
    }

    [Fact]
    public async Task CodeNameCountryAndContact_StayRequired()
    {
        var noCountry = Valid();
        noCountry.Country = " ";
        var noContact = Valid();
        noContact.ContactName = null;

        var first = await Handler().HandleAsync(noCountry, CancellationToken.None);
        var second = await Handler().HandleAsync(noContact, CancellationToken.None);

        Assert.Contains(first.IssuesOf<SupplierIssue>(), issue => issue.Field == SupplierField.Country && issue.Kind == SupplierIssueKind.Missing);
        Assert.Contains(second.IssuesOf<SupplierIssue>(), issue => issue.Field == SupplierField.ContactName && issue.Kind == SupplierIssueKind.Missing);
        Assert.Empty(_repository.Suppliers);
    }

    [Fact]
    public async Task BankDetails_AreStored_AndSwiftIsValidatedAndUppercased()
    {
        var bad = Valid();
        bad.SwiftCode = "ABC";
        var rejected = await Handler().HandleAsync(bad, CancellationToken.None);
        Assert.Equal(SupplierError.InvalidSwift, rejected.Error);
        Assert.Empty(_repository.Suppliers);

        var good = Valid();
        good.BankName = "Bank of China";
        good.SwiftCode = "bkchcnbj";
        var accepted = await Handler().HandleAsync(good, CancellationToken.None);

        Assert.True(accepted.IsSuccess);
        var supplier = Assert.Single(_repository.Suppliers);
        Assert.Equal("Bank of China", supplier.BankName);
        Assert.Equal("BKCHCNBJ", supplier.SwiftCode);
    }

    [Fact]
    public async Task Create_WritesAnAuditEntry()
    {
        var audit = new MemoryAudit();

        var result = await new CreateSupplierCommandHandler(_repository, NullLogger<CreateSupplierCommandHandler>.Instance, audit: audit)
            .HandleAsync(Valid(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var entry = Assert.Single(audit.Entries);
        Assert.Equal(Pancras.Domain.Auditing.AuditActions.SupplierCreated, entry.Action);
        Assert.Equal(result.Value!.Id.ToString("D"), entry.TargetId);
    }

    private CreateSupplierCommandHandler Handler() =>
        new(_repository, NullLogger<CreateSupplierCommandHandler>.Instance);

    private static CreateSupplierCommand Valid(string? code = null, string? name = null, string? email = null, string? phone = null, Pancras.Domain.Employees.EmployeeActor? actor = null) =>
        new()
        {
            Actor = actor ?? SupplierActors.Admin,
            Code = code ?? "NCC01",
            Name = name ?? "Kim An",
            Country = "Trung Quốc",
            ContactName = "Nguyễn An",
            Phone = phone ?? "0901000111",
            Email = email ?? "a@b.co",
            Account = "123456"
        };
}
