using Microsoft.Extensions.Logging.Abstractions;
using Pancras.Application.Suppliers;
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

        var cards = await Handler().HandleAsync(CancellationToken.None);

        var card = Assert.Single(cards.Cards);
        Assert.Equal("NCC01", card.Code);
        Assert.Equal("Kim An", card.Name);
        Assert.Equal("Trung Quốc", card.Country);
        Assert.Equal("Nguyễn An", card.ContactName);
        Assert.Equal("0901000111", card.Phone);
        Assert.Equal("a@b.co", card.Email);
        Assert.Equal("123456", card.Account);
        Assert.Equal(2, card.PurchaseOrderCount);
        Assert.Equal(1, card.LotsNotReceived);
    }

    [Fact]
    public async Task MissingStats_AreZero()
    {
        _repository.Suppliers.Add(Sample());

        var cards = await Handler().HandleAsync(CancellationToken.None);

        var card = Assert.Single(cards.Cards);
        Assert.Equal(0, card.PurchaseOrderCount);
        Assert.Equal(0, card.LotsNotReceived);
    }

    [Fact]
    public async Task Search_MatchesCodeOrName_IgnoringCase()
    {
        _repository.Suppliers.Add(Supplier.Create("NCC01", "Kim An", "Trung Quốc", "Nguyễn An", "0901000001", "a@b.co", "123"));
        _repository.Suppliers.Add(Supplier.Create("NCC02", "Kim Lan", "Việt Nam", "Lan", "0902000002", "b@c.co", "456"));
        var deleted = Supplier.Create("NCC01X", "Kim An cũ", "Trung Quốc", "An", "0903000003", "c@d.co", "789");
        deleted.MarkDeleted();
        _repository.Suppliers.Add(deleted);

        var byCode = await Handler().HandleAsync(SupplierListFilter.From("ncc01"), CancellationToken.None);
        var codeCard = Assert.Single(byCode.Cards);
        Assert.Equal("NCC01", codeCard.Code);

        var byName = await Handler().HandleAsync(SupplierListFilter.From("lan"), CancellationToken.None);
        var nameCard = Assert.Single(byName.Cards);
        Assert.Equal("NCC02", nameCard.Code);

        var shared = await Handler().HandleAsync(SupplierListFilter.From("kim"), CancellationToken.None);
        Assert.Equal(2, shared.Cards.Count);

        var none = await Handler().HandleAsync(SupplierListFilter.From("ZZZ"), CancellationToken.None);
        Assert.Empty(none.Cards);

        var blank = await Handler().HandleAsync(SupplierListFilter.From("  "), CancellationToken.None);
        Assert.Equal(2, blank.Cards.Count);
    }

    [Fact]
    public async Task Page_ReturnsOneSlice_AndClampsPastTheEnd()
    {
        _repository.Suppliers.Add(Supplier.Create("A1", "Alpha", "Việt Nam", "An", "0901000001", "a@b.co", "1"));
        _repository.Suppliers.Add(Supplier.Create("A2", "Beta", "Việt Nam", "An", "0902000002", "b@c.co", "2"));
        _repository.Suppliers.Add(Supplier.Create("A3", "Gamma", "Việt Nam", "An", "0903000003", "c@d.co", "3"));

        var first = await Handler().HandleAsync(
            SupplierListFilter.From(null),
            new SupplierPageRequest(1, 2),
            CancellationToken.None);
        Assert.Equal(2, first.Cards.Count);
        Assert.Equal("Alpha", first.Cards[0].Name);
        Assert.Equal("Beta", first.Cards[1].Name);
        Assert.Equal(3, first.TotalCount);
        Assert.Equal(2, first.TotalPages);

        var second = await Handler().HandleAsync(
            SupplierListFilter.From(null),
            new SupplierPageRequest(2, 2),
            CancellationToken.None);
        var lastCard = Assert.Single(second.Cards);
        Assert.Equal("Gamma", lastCard.Name);

        var past = await Handler().HandleAsync(
            SupplierListFilter.From(null),
            new SupplierPageRequest(9, 2),
            CancellationToken.None);
        Assert.Equal(2, past.PageNumber);
        Assert.Equal("Gamma", Assert.Single(past.Cards).Name);
    }

    private ListSupplierCardsHandler Handler() =>
        new(_repository, _stats, NullLogger<ListSupplierCardsHandler>.Instance);

    private static Supplier Sample() =>
        Supplier.Create("NCC01", "Kim An", "Trung Quốc", "Nguyễn An", "0901000111", "a@b.co", "123456");
}
