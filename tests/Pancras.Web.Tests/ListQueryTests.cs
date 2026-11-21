using Pancras.Web.Formatting;

namespace Pancras.Web.Tests;

/// <summary>Bộ dựng địa chỉ danh sách dùng chung (docs/03-ui-ux/components.md mục 10).</summary>
public class ListQueryTests
{
    [Fact]
    public void EmptyQuery_IsJustThePath()
    {
        Assert.Equal("/catalog", new ListQuery("/catalog").Add("q", "  ").Add("page", 1, 1).AddPage("page", 1).ToString());
    }

    [Fact]
    public void Values_AreTrimmedEncodedAndKeptInOrder()
    {
        var url = new ListQuery("/purchasing")
            .Add("q", "  PO 01&x ")
            .Add("supplier", (Guid?)Guid.Parse("01890000-0000-7000-8000-000000000001"))
            .Add("add", true)
            .Add("size", 50, 20)
            .AddPage("pageNumber", 3)
            .ToString();

        Assert.Equal("/purchasing?q=PO%2001%26x&supplier=01890000-0000-7000-8000-000000000001&add=true&size=50&pageNumber=3", url);
    }

    [Fact]
    public void EmptyGuidAndFalseFlag_AreLeftOut()
    {
        Assert.Equal("/x", new ListQuery("/x").Add("id", (Guid?)Guid.Empty).Add("id", (Guid?)null).Add("add", false).ToString());
    }

    [Fact]
    public void WithHandler_PutsTheHandlerFirst_AndKeepsTheListState()
    {
        var query = new ListQuery("/suppliers").Add("q", "abc").AddPage("page", 2);

        Assert.Equal("/suppliers?handler=Stop&q=abc&page=2", query.WithHandler("Stop"));
        Assert.Equal("/suppliers?q=abc&page=2", query.WithHandler(null));
        Assert.Equal("/suppliers?handler=Stop", new ListQuery("/suppliers").WithHandler("Stop"));
    }
}
