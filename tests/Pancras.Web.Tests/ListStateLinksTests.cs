using System.Text.RegularExpressions;

namespace Pancras.Web.Tests;

/// <summary>
/// Lỗi UAT: chọn đơn mua mất bộ lọc vì link viết cứng. Luật (docs/03-ui-ux/components.md mục 10, ui-checklist U4):
/// link trong trang Mua hàng tới đơn hoặc thao tác trên đơn phải đi qua <c>OrderUrl</c>, không viết chuỗi URL cứng có tham số.
/// </summary>
public class ListStateLinksTests
{
    private static readonly string PurchasingPages = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../src/Pancras.Web/Pages/Purchasing"));

    [Fact]
    public void PurchasingViews_DoNotHardcodeOrderLinks()
    {
        var offenders = Directory.EnumerateFiles(PurchasingPages, "*.cshtml")
            .SelectMany(file => Regex.Matches(File.ReadAllText(file), @"href=""/purchasing\?(?!form=1|handler=Download)[^""]*""")
                .Select(match => Path.GetFileName(file) + ": " + match.Value))
            .ToArray();

        Assert.True(offenders.Length == 0, "Dùng Model.OrderUrl(...) để giữ bộ lọc:\n" + string.Join("\n", offenders));
    }
}
