using Microsoft.Extensions.Logging.Abstractions;
using Pancras.Application.Common;
using Pancras.Application.Suppliers;
using Pancras.Domain.Suppliers;

namespace Pancras.Application.Tests;

public class UpdateSupplierCommandHandlerTests
{
    private readonly FakeSupplierRepository _repository = new();

    [Fact]
    public async Task ValidEdit_ReplacesTheProfile()
    {
        var supplier = AddSample();
        var result = await Handler().HandleAsync(Edit(supplier.Id, name: "Tên mới", email: "b@c.co"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("Tên mới", supplier.Name);
        Assert.Equal("b@c.co", supplier.Email);
        Assert.Equal("NCC01", supplier.Code);
    }

    [Fact]
    public async Task StaleVersion_IsRejected_AndChangesNothing()
    {
        var supplier = AddSample();
        var command = Edit(supplier.Id, name: "Tên mới");
        command.Version = supplier.Version + 1;

        var result = await Handler().HandleAsync(command, CancellationToken.None);

        Assert.Equal(SupplierError.VersionConflict, result.SupplierError);
        Assert.Equal("Kim An", supplier.Name);
    }

    [Fact]
    public async Task SameCode_IsAllowed()
    {
        var supplier = AddSample();
        var result = await Handler().HandleAsync(Edit(supplier.Id, code: "ncc01"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("ncc01", supplier.Code);
    }

    [Fact]
    public async Task DuplicateCode_DoesNotChangeTheProfile()
    {
        var supplier = AddSample();
        _repository.Suppliers.Add(Supplier.Create("NCC02", "Khác", "Trung Quốc", "Lan", "0902000002", "b@c.co", "999"));

        var result = await Handler().HandleAsync(Edit(supplier.Id, code: "ncc02"), CancellationToken.None);

        Assert.Equal(SupplierError.DuplicateCode, result.SupplierError);
        Assert.Equal("Kim An", supplier.Name);
        Assert.Equal("NCC01", supplier.Code);
    }

    [Fact]
    public async Task BlankName_DoesNotChangeTheProfile()
    {
        var supplier = AddSample();
        var result = await Handler().HandleAsync(Edit(supplier.Id, name: " "), CancellationToken.None);

        Assert.Equal(SupplierError.Incomplete, result.SupplierError);
        Assert.Equal("Kim An", supplier.Name);
    }

    [Fact]
    public async Task InactiveSupplier_IsNotFound_AndStaysInactive()
    {
        var supplier = AddSample();
        supplier.Deactivate();

        var result = await Handler().HandleAsync(Edit(supplier.Id, name: "Tên mới"), CancellationToken.None);

        Assert.Equal(SupplierError.NotFound, result.SupplierError);
        Assert.True(supplier.IsInactive);
        Assert.Equal("Kim An", supplier.Name);
    }

    [Fact]
    public async Task UniqueCollisionOnSave_IsDuplicateCode()
    {
        var supplier = AddSample();
        _repository.RejectNextSave = true;

        var result = await Handler().HandleAsync(Edit(supplier.Id, name: "Tên mới"), CancellationToken.None);

        Assert.Equal(SupplierError.DuplicateCode, result.SupplierError);
    }

    [Fact]
    public async Task MissingSupplier_IsNotFound()
    {
        var result = await Handler().HandleAsync(Edit(Guid.CreateVersion7()), CancellationToken.None);

        Assert.Equal(SupplierError.NotFound, result.SupplierError);
    }

    [Fact]
    public async Task OnlyManagersCanEdit()
    {
        var supplier = AddSample();
        var command = Edit(supplier.Id, name: "Tên mới");
        command.Actor = SupplierActors.Keeper;

        var result = await Handler().HandleAsync(command, CancellationToken.None);

        Assert.Equal(SupplierError.Forbidden, result.SupplierError);
        Assert.Equal("Kim An", supplier.Name);
    }

    [Fact]
    public async Task Edit_RecordsWhatChanged_WithoutLeakingTheFullAccountNumber()
    {
        var supplier = AddSample();
        var audit = new MemoryAudit();
        var command = Edit(supplier.Id, name: "Tên mới");
        command.Account = "987654321";

        var result = await new UpdateSupplierCommandHandler(_repository, NullLogger<UpdateSupplierCommandHandler>.Instance, audit: audit)
            .HandleAsync(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        var entry = Assert.Single(audit.Entries);
        Assert.Equal(Pancras.Domain.Auditing.AuditActions.SupplierUpdated, entry.Action);
        Assert.Contains("\"Kim An\" → \"Tên mới\"", entry.Detail, StringComparison.Ordinal);
        Assert.Contains("••••3456 → ••••4321", entry.Detail, StringComparison.Ordinal);
        Assert.DoesNotContain("987654321", entry.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RenamingTheCodeToOneHeldByAnInactiveSupplier_IsRefused()
    {
        var supplier = AddSample();
        var old = Supplier.Create("NCC09", "Cũ", "Ý", "Lan", "", "", "");
        old.Deactivate();
        _repository.Suppliers.Add(old);

        var result = await Handler().HandleAsync(Edit(supplier.Id, code: "ncc09"), CancellationToken.None);

        Assert.Equal(SupplierError.DuplicateCode, result.SupplierError);
        Assert.Contains(result.SupplierIssues, issue => issue.Kind == SupplierIssueKind.DuplicateInactive);
        Assert.Equal("NCC01", supplier.Code);
    }

    private UpdateSupplierCommandHandler Handler() =>
        new(_repository, NullLogger<UpdateSupplierCommandHandler>.Instance);

    private Supplier AddSample()
    {
        var supplier = Supplier.Create("NCC01", "Kim An", "Trung Quốc", "Nguyễn An", "0901000111", "a@b.co", "123456");
        _repository.Suppliers.Add(supplier);
        return supplier;
    }

    private static UpdateSupplierCommand Edit(
        Guid id,
        string? code = null,
        string? name = null,
        string? email = null) =>
        new()
        {
            Actor = SupplierActors.Admin,
            Id = id,
            Code = code ?? "NCC01",
            Name = name ?? "Kim An",
            Country = "Trung Quốc",
            ContactName = "Nguyễn An",
            Phone = "0901000111",
            Email = email ?? "a@b.co",
            Account = "123456"
        };
}
