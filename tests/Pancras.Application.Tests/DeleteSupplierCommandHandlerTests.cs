using Microsoft.Extensions.Logging.Abstractions;
using Pancras.Application.Common;
using Pancras.Application.Suppliers;
using Pancras.Domain.Suppliers;

namespace Pancras.Application.Tests;

public class DeleteSupplierCommandHandlerTests
{
    private readonly FakeSupplierRepository _repository = new();

    [Fact]
    public async Task Delete_HidesTheSupplier_AndKeepsTheRow()
    {
        var supplier = Supplier.Create("NCC01", "Kim An", "Trung Quốc", "Nguyễn An", "0901000111", "a@b.co", "123456");
        _repository.Suppliers.Add(supplier);

        var result = await new DeleteSupplierCommandHandler(_repository, NullLogger<DeleteSupplierCommandHandler>.Instance)
            .HandleAsync(supplier.Id, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(supplier.IsDeleted);
        Assert.Equal("NCC01", supplier.Code);
        Assert.Empty(await _repository.ListAsync(CancellationToken.None));

        var options = await new ListSuppliersForPurchaseHandler(
            _repository,
            NullLogger<ListSuppliersForPurchaseHandler>.Instance).HandleAsync(CancellationToken.None);
        Assert.Empty(options);
    }

    [Fact]
    public async Task DeletedCode_CanBeUsedAgain()
    {
        var supplier = Supplier.Create("NCC01", "Kim An", "Trung Quốc", "Nguyễn An", "0901000111", "a@b.co", "123456");
        _repository.Suppliers.Add(supplier);
        supplier.MarkDeleted();

        var result = await new CreateSupplierCommandHandler(_repository, NullLogger<CreateSupplierCommandHandler>.Instance)
            .HandleAsync(new CreateSupplierCommand
            {
                Code = "NCC01",
                Name = "Kim An mới",
                Country = "Trung Quốc",
                ContactName = "Nguyễn An",
                Phone = "0901000111",
                Email = "a@b.co",
                Account = "123456"
            }, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, _repository.Suppliers.Count);
        Assert.Single(await _repository.ListAsync(CancellationToken.None));
    }

    [Fact]
    public async Task MissingSupplier_IsNotFound()
    {
        var result = await new DeleteSupplierCommandHandler(_repository, NullLogger<DeleteSupplierCommandHandler>.Instance)
            .HandleAsync(Guid.CreateVersion7(), CancellationToken.None);

        Assert.Equal(SupplierError.NotFound, result.SupplierError);
    }
}
