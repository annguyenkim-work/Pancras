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

        Assert.Equal(SupplierError.Incomplete, result.SupplierError);
        Assert.Contains(result.SupplierIssues, issue => issue.Field == SupplierField.Name && issue.Kind == SupplierIssueKind.Missing);
        Assert.Empty(_repository.Suppliers);
    }

    [Fact]
    public async Task SeveralProblems_AreAllReported()
    {
        var result = await Handler().HandleAsync(Valid(name: " ", email: "khong-phai-email"), CancellationToken.None);

        Assert.Equal(SupplierError.Incomplete, result.SupplierError);
        Assert.Contains(result.SupplierIssues, issue => issue.Field == SupplierField.Name && issue.Kind == SupplierIssueKind.Missing);
        Assert.Contains(result.SupplierIssues, issue => issue.Field == SupplierField.Email && issue.Kind == SupplierIssueKind.InvalidEmail);
        Assert.Empty(_repository.Suppliers);
    }

    [Fact]
    public async Task InvalidEmail_DoesNotInsert()
    {
        var result = await Handler().HandleAsync(Valid(email: "khong-phai-email"), CancellationToken.None);

        Assert.Equal(SupplierError.InvalidEmail, result.SupplierError);
        Assert.Empty(_repository.Suppliers);
    }

    [Fact]
    public async Task InvalidPhone_DoesNotInsert()
    {
        var letters = await Handler().HandleAsync(Valid(phone: "abc"), CancellationToken.None);
        var tooShort = await Handler().HandleAsync(Valid(phone: "0901"), CancellationToken.None);
        var international = await Handler().HandleAsync(Valid(phone: "+86 138 0000 0000"), CancellationToken.None);

        Assert.Equal(SupplierError.InvalidPhone, letters.SupplierError);
        Assert.Contains(letters.SupplierIssues, issue => issue.Field == SupplierField.Phone && issue.Kind == SupplierIssueKind.InvalidPhone);
        Assert.Equal(SupplierError.InvalidPhone, tooShort.SupplierError);
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

        Assert.Equal(SupplierError.TooLong, result.SupplierError);
        Assert.Empty(_repository.Suppliers);
    }

    [Fact]
    public async Task TooLongCode_DoesNotInsert()
    {
        var result = await Handler().HandleAsync(Valid(code: new string('A', 33)), CancellationToken.None);

        Assert.Equal(SupplierError.TooLong, result.SupplierError);
        Assert.Empty(_repository.Suppliers);
    }

    [Fact]
    public async Task DuplicateCode_DoesNotInsertAgain()
    {
        var handler = Handler();
        var first = await handler.HandleAsync(Valid(), CancellationToken.None);
        var second = await handler.HandleAsync(Valid(code: "ncc01", name: "Nhà khác"), CancellationToken.None);

        Assert.True(first.IsSuccess);
        Assert.Equal(SupplierError.DuplicateCode, second.SupplierError);
        Assert.Contains(second.SupplierIssues, issue => issue.Field == SupplierField.Code && issue.Kind == SupplierIssueKind.Duplicate);
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

    private CreateSupplierCommandHandler Handler() =>
        new(_repository, NullLogger<CreateSupplierCommandHandler>.Instance);

    private static CreateSupplierCommand Valid(string? code = null, string? name = null, string? email = null, string? phone = null) =>
        new()
        {
            Code = code ?? "NCC01",
            Name = name ?? "Kim An",
            Country = "Trung Quốc",
            ContactName = "Nguyễn An",
            Phone = phone ?? "0901000111",
            Email = email ?? "a@b.co",
            Account = "123456"
        };
}
