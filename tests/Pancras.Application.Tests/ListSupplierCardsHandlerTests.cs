using Microsoft.Extensions.Logging.Abstractions;
using Pancras.Application.Suppliers;
using Pancras.Domain.Employees;
using Pancras.Domain.Suppliers;

namespace Pancras.Application.Tests;

public class ListSupplierCardsHandlerTests
{
    private readonly FakeSupplierRepository _repository = new();
    private readonly FakeSupplierPurchaseStatsReader _stats = new();

    [Fact]
    public async Task Cards_CarryPurchaseCounts_AndProfileFields()
    {
        var supplier = Sample();
        _repository.Suppliers.Add(supplier);
        _stats.Stats[supplier.Id] = new SupplierPurchaseStats(2, 1);

        var cards = await List(ListSuppliersQuery.From(null), SupplierActors.Accountant);

        var card = Assert.Single(cards.Cards);
        Assert.Equal("NCC01", card.Code);
        Assert.Equal("Kim An", card.Name);
        Assert.Equal("Trung Quốc", card.Country);
        Assert.Equal("Nguyễn An", card.ContactName);
        Assert.Equal("0901000111", card.Phone);
        Assert.Equal("a@b.co", card.Email);
        Assert.Equal(2, card.PurchaseOrderCount);
        Assert.Equal(1, card.LotsNotReceived);
        Assert.True(card.IsActive);
    }

    [Fact]
    public async Task TheAccountNumber_IsMaskedForManagers_AndHiddenFromWarehouseRoles()
    {
        _repository.Suppliers.Add(Sample());

        var manager = Assert.Single((await List(ListSuppliersQuery.From(null), SupplierActors.Admin)).Cards);
        var keeper = Assert.Single((await List(ListSuppliersQuery.From(null), SupplierActors.Keeper)).Cards);

        Assert.Equal("••••3456", manager.AccountMask);
        Assert.Equal(string.Empty, keeper.AccountMask);
        Assert.Equal("Kim An", keeper.Name);
    }

    [Fact]
    public async Task RolesOutsideTheDirectory_GetNothing()
    {
        _repository.Suppliers.Add(Sample());

        var sales = await List(ListSuppliersQuery.From(null), SupplierActors.Sales);

        Assert.Empty(sales.Cards);
        Assert.Equal(0, sales.TotalCount);
    }

    [Fact]
    public async Task MissingStats_AreZero()
    {
        _repository.Suppliers.Add(Sample());

        var cards = await List(ListSuppliersQuery.From(null), SupplierActors.Admin);

        var card = Assert.Single(cards.Cards);
        Assert.Equal(0, card.PurchaseOrderCount);
        Assert.Equal(0, card.LotsNotReceived);
    }

    [Fact]
    public async Task Search_MatchesCodeNameContactEmailPhoneAndCountry_IgnoringCase()
    {
        _repository.Suppliers.Add(Supplier.Create("NCC01", "Kim An", "Trung Quốc", "Nguyễn An", "0901000001", "a@b.co", "123"));
        _repository.Suppliers.Add(Supplier.Create("NCC02", "Kim Lan", "Việt Nam", "Lan", "0902000002", "b@c.co", "456"));
        var inactive = Supplier.Create("NCC01X", "Kim An cũ", "Trung Quốc", "An", "0903000003", "c@d.co", "789");
        inactive.Deactivate();
        _repository.Suppliers.Add(inactive);

        Assert.Equal("NCC01", Assert.Single((await List(ListSuppliersQuery.From("ncc01"))).Cards).Code);
        Assert.Equal("NCC02", Assert.Single((await List(ListSuppliersQuery.From("lan"))).Cards).Code);
        Assert.Equal(2, (await List(ListSuppliersQuery.From("kim"))).Cards.Count);
        Assert.Empty((await List(ListSuppliersQuery.From("ZZZ"))).Cards);
        Assert.Equal(2, (await List(ListSuppliersQuery.From("  "))).Cards.Count);

        Assert.Equal("NCC02", Assert.Single((await List(ListSuppliersQuery.From("b@c.co"))).Cards).Code);
        Assert.Equal("NCC01", Assert.Single((await List(ListSuppliersQuery.From("0901000001"))).Cards).Code);
        Assert.Equal("NCC02", Assert.Single((await List(ListSuppliersQuery.From("việt"))).Cards).Code);
    }

    [Fact]
    public async Task Filters_ByCountryAndStatus()
    {
        _repository.Suppliers.Add(Supplier.Create("NCC01", "Kim An", "Trung Quốc", "An", "", "", ""));
        _repository.Suppliers.Add(Supplier.Create("NCC02", "Kim Lan", "Ý", "Lan", "", "", ""));
        var inactive = Supplier.Create("NCC03", "Cũ", "Ý", "Cũ", "", "", "");
        inactive.Deactivate();
        _repository.Suppliers.Add(inactive);

        Assert.Equal("NCC02", Assert.Single((await List(ListSuppliersQuery.From(null, SupplierStatusFilter.Active, "Ý"))).Cards).Code);
        Assert.Equal("NCC03", Assert.Single((await List(ListSuppliersQuery.From(null, SupplierStatusFilter.Inactive))).Cards).Code);
        Assert.Equal(3, (await List(ListSuppliersQuery.From(null, SupplierStatusFilter.All))).Cards.Count);

        var countries = await new ListSupplierCardsHandler(_repository, _stats, NullLogger<ListSupplierCardsHandler>.Instance)
            .ListCountriesAsync(SupplierActors.Admin, CancellationToken.None);
        Assert.Equal(2, countries.Count);
        Assert.Contains("Ý", countries);
        Assert.Contains("Trung Quốc", countries);
    }

    [Fact]
    public async Task Page_ReturnsOneSlice_AndClampsPastTheEnd()
    {
        _repository.Suppliers.Add(Supplier.Create("A1", "Alpha", "Việt Nam", "An", "0901000001", "a@b.co", "1"));
        _repository.Suppliers.Add(Supplier.Create("A2", "Beta", "Việt Nam", "An", "0902000002", "b@c.co", "2"));
        _repository.Suppliers.Add(Supplier.Create("A3", "Gamma", "Việt Nam", "An", "0903000003", "c@d.co", "3"));

        var first = await List(ListSuppliersQuery.From(null).WithPage(1, 2), SupplierActors.Admin);
        Assert.Equal(2, first.Cards.Count);
        Assert.Equal("Alpha", first.Cards[0].Name);
        Assert.Equal("Beta", first.Cards[1].Name);
        Assert.Equal(3, first.TotalCount);
        Assert.Equal(2, first.TotalPages);

        var second = await List(ListSuppliersQuery.From(null).WithPage(2, 2), SupplierActors.Admin);
        Assert.Equal("Gamma", Assert.Single(second.Cards).Name);

        var past = await List(ListSuppliersQuery.From(null).WithPage(9, 2), SupplierActors.Admin);
        Assert.Equal(2, past.PageNumber);
        Assert.Equal("Gamma", Assert.Single(past.Cards).Name);
    }

    [Fact]
    public async Task Profile_IsForManagersOnly_AndIncludesBankDetails()
    {
        var supplier = Supplier.Create("NCC01", "Kim An", "Trung Quốc", "An", "", "", "123456", "Bank of China", "BKCHCNBJ");
        _repository.Suppliers.Add(supplier);
        _stats.Stats[supplier.Id] = new SupplierPurchaseStats(4, 1);
        var handler = new GetSupplierProfileHandler(_repository, NullLogger<GetSupplierProfileHandler>.Instance, _stats);

        var keeper = await handler.HandleAsync(supplier.Id, SupplierActors.Keeper, CancellationToken.None);
        var admin = await handler.HandleAsync(supplier.Id, SupplierActors.Admin, CancellationToken.None);

        Assert.Equal(Pancras.Application.Common.SupplierError.Forbidden, keeper.Error);
        Assert.Equal("123456", admin.Value!.Account);
        Assert.Equal("Bank of China", admin.Value.BankName);
        Assert.Equal("BKCHCNBJ", admin.Value.SwiftCode);
        Assert.Equal(4, admin.Value.PurchaseOrderCount);
        Assert.Equal(1, admin.Value.LotsNotReceived);
    }

    private Task<SupplierCardPage> List(ListSuppliersQuery query, EmployeeActor? actor = null) =>
        new ListSupplierCardsHandler(_repository, _stats, NullLogger<ListSupplierCardsHandler>.Instance)
            .HandleAsync(query, actor ?? SupplierActors.Admin, CancellationToken.None);

    private static Supplier Sample() =>
        Supplier.Create("NCC01", "Kim An", "Trung Quốc", "Nguyễn An", "0901000111", "a@b.co", "123456");
}
