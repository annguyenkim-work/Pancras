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

        var options = await handler.HandleAsync(SupplierActors.Accountant, CancellationToken.None);

        var option = Assert.Single(options);
        Assert.Equal(supplier.Id, option.Id);
        Assert.Equal("NCC01", option.Code);
        Assert.Equal("Kim An", option.Name);
    }

    [Fact]
    public async Task OnlyManagersGetTheChoices_AndInactiveSuppliersAreLeftOut()
    {
        var repository = new FakeSupplierRepository();
        repository.Suppliers.Add(Supplier.Create("NCC01", "Kim An", "Trung Quốc", "Nguyễn An", "", "", ""));
        var inactive = Supplier.Create("NCC02", "Cũ", "Ý", "Lan", "", "", "");
        inactive.Deactivate();
        repository.Suppliers.Add(inactive);
        var handler = new ListSuppliersForPurchaseHandler(repository, NullLogger<ListSuppliersForPurchaseHandler>.Instance);

        Assert.Empty(await handler.HandleAsync(SupplierActors.Keeper, CancellationToken.None));
        Assert.Equal("NCC01", Assert.Single(await handler.HandleAsync(SupplierActors.Admin, CancellationToken.None)).Code);
    }
}
