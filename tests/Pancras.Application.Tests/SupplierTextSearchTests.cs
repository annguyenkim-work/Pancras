using Pancras.Application.Suppliers;

namespace Pancras.Application.Tests;

public class SupplierTextSearchTests
{
    [Fact]
    public void ContainsPattern_EscapesLikeWildcards()
    {
        Assert.Equal("%ncc%", SupplierTextSearch.ContainsPattern(" ncc "));
        Assert.Equal("%100\\%%", SupplierTextSearch.ContainsPattern("100%"));
        Assert.Equal("%a\\_b%", SupplierTextSearch.ContainsPattern("a_b"));
        Assert.Equal("%a\\\\b%", SupplierTextSearch.ContainsPattern("a\\b"));
    }
}
