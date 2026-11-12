using Pancras.Domain.Suppliers;

namespace Pancras.Domain.Tests;

public class SupplierTests
{
    [Fact]
    public void Create_TrimsFields_AndNormalizesCode()
    {
        var supplier = Supplier.Create(
            " ncc01 ",
            " Kim An ",
            " Trung Quốc ",
            " Nguyễn An ",
            " 0901000111 ",
            " a@b.co ",
            " 123456 ");

        Assert.Equal("ncc01", supplier.Code);
        Assert.Equal("NCC01", supplier.NormalizedCode);
        Assert.Equal("Kim An", supplier.Name);
        Assert.Equal("Trung Quốc", supplier.Country);
        Assert.Equal("Nguyễn An", supplier.ContactName);
        Assert.Equal("0901000111", supplier.Phone);
        Assert.Equal("a@b.co", supplier.Email);
        Assert.Equal("123456", supplier.Account);
        Assert.NotEqual(Guid.Empty, supplier.Id);
    }

    [Fact]
    public void Create_RejectsBlankName()
    {
        Assert.Throws<ArgumentException>(() => Valid(name: " "));
    }

    [Fact]
    public void Create_RejectsEmailWithoutDomainDot()
    {
        Assert.Throws<ArgumentException>(() => Valid(email: "a@b"));
    }

    [Fact]
    public void Create_AcceptsLocalAndInternationalPhones()
    {
        Assert.Equal("0901234567", Valid(phone: " 0901234567 ").Phone);
        Assert.Equal("+84 901 234 567", Valid(phone: "+84 901 234 567").Phone);
        Assert.Equal("+86-138-0000-0000", Valid(phone: "+86-138-0000-0000").Phone);
    }

    [Fact]
    public void Create_RejectsPhoneThatIsNotANumber()
    {
        Assert.Throws<ArgumentException>(() => Valid(phone: "abc"));
        Assert.Throws<ArgumentException>(() => Valid(phone: "0901"));
        Assert.Throws<ArgumentException>(() => Valid(phone: "1234567890123456"));
    }

    [Fact]
    public void Update_ReplacesTheProfile()
    {
        var supplier = Valid();

        supplier.Update("NCC02", "Tên mới", "Việt Nam", "Lan", "0902000002", "b@c.co", "999");

        Assert.Equal("NCC02", supplier.Code);
        Assert.Equal("NCC02", supplier.NormalizedCode);
        Assert.Equal("Tên mới", supplier.Name);
        Assert.Equal("b@c.co", supplier.Email);
        Assert.False(supplier.IsDeleted);
    }

    [Fact]
    public void Update_RejectsBlankName_WithoutChangingTheProfile()
    {
        var supplier = Valid();

        Assert.Throws<ArgumentException>(() => supplier.Update("NCC01", " ", "Việt Nam", "Lan", "0902000002", "b@c.co", "999"));
        Assert.Equal("Kim An", supplier.Name);
        Assert.Equal("NCC01", supplier.Code);
    }

    [Fact]
    public void MarkDeleted_KeepsTheRowAndHidesIt()
    {
        var supplier = Valid();

        supplier.MarkDeleted();

        Assert.True(supplier.IsDeleted);
        Assert.Equal("NCC01", supplier.Code);
    }

    [Fact]
    public void Email_WithDomainDot_IsAddress()
    {
        Assert.True(SupplierEmail.IsAddress("a@b.co"));
        Assert.False(SupplierEmail.IsAddress("a@b"));
    }

    private static Supplier Valid(string? name = null, string? email = null, string? phone = null) =>
        Supplier.Create(
            "NCC01",
            name ?? "Kim An",
            "Trung Quốc",
            "Nguyễn An",
            phone ?? "0901000111",
            email ?? "a@b.co",
            "123456");
}
