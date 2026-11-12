using Microsoft.Extensions.Logging.Abstractions;
using Pancras.Application.Suppliers;
using Pancras.Domain.Suppliers;

namespace Pancras.Application.Tests;

public class ListSuppliersForPurchaseHandlerTests
{
    [Fact]
    public async Task IncludesSavedSupplier()
    {
        var repository = new FakeSupplierRepository();
        var supplier = Supplier.Create("NCC01", "Kim An", "Trung Quốc", "Nguyễn An", "0901000111", "a@b.co", "123456");
        repository.Suppliers.Add(supplier);
        var handler = new ListSuppliersForPurchaseHandler(
            repository,
            NullLogger<ListSuppliersForPurchaseHandler>.Instance);

        var options = await handler.HandleAsync(CancellationToken.None);

        var option = Assert.Single(options);
        Assert.Equal(supplier.Id, option.Id);
        Assert.Equal("NCC01", option.Code);
        Assert.Equal("Kim An", option.Name);
    }
}
