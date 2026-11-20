namespace Pancras.Web.Tests;

/// <summary>Nút thu gọn sidebar desktop (docs/03-ui-ux/app-shell.md): nút nằm ở header, hành vi ở ui.js, không JS inline.</summary>
public class SidebarCollapseTests
{
    private static readonly string WebRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../src/Pancras.Web"));

    [Fact]
    public void Layout_HasToggleButton_AndMarksSidebarAndContent()
    {
        var layout = File.ReadAllText(Path.Combine(WebRoot, "Pages/Shared/_Layout.cshtml"));

        Assert.Contains("data-sidebar-toggle", layout, StringComparison.Ordinal);
        Assert.Contains("id=\"app-sidebar\"", layout, StringComparison.Ordinal);
        Assert.Contains("id=\"app-content\"", layout, StringComparison.Ordinal);
    }

    [Fact]
    public void UiScript_HandlesToggle_AndRemembersChoiceSafely()
    {
        var script = File.ReadAllText(Path.Combine(WebRoot, "wwwroot/js/ui.js"));

        Assert.Contains("data-sidebar-toggle", script, StringComparison.Ordinal);
        Assert.Contains("localStorage", script, StringComparison.Ordinal);
        Assert.Contains("try", script, StringComparison.Ordinal);
    }

    [Fact]
    public void MainContent_IsCenteredWithAMinAndMaxWidth_OnLaptopAndDesktop()
    {
        // Lỗi UAT: khi thu nhỏ menu, nội dung dính trái (max-w-7xl nhưng không mx-auto) và hở khoảng trống bên phải.
        var layout = File.ReadAllText(Path.Combine(WebRoot, "Pages/Shared/_Layout.cshtml"));
        var css = File.ReadAllText(Path.Combine(WebRoot, "wwwroot/css/site.css"));

        var main = System.Text.RegularExpressions.Regex.Match(layout, "<main class=\"([^\"]*)\"").Groups[1].Value.Split(' ');
        Assert.Contains("mx-auto", main);
        Assert.Contains("w-full", main);
        Assert.Contains("max-w-screen-2xl", main);
        Assert.Contains("lg:min-w-[40rem]", main);
        Assert.Contains(".mx-auto", css, StringComparison.Ordinal);
        Assert.Contains("max-w-screen-2xl", css, StringComparison.Ordinal);
        Assert.Contains("lg\\:min-w-\\[40rem\\]", css, StringComparison.Ordinal);
    }

    [Fact]
    public void CollapsedClasses_ExistInBuiltCss()
    {
        var css = File.ReadAllText(Path.Combine(WebRoot, "wwwroot/css/site.css"));

        Assert.Contains("lg\\:-translate-x-full", css, StringComparison.Ordinal);
        Assert.Contains("lg\\:pl-0", css, StringComparison.Ordinal);
    }
}
