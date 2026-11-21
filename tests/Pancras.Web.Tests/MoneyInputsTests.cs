using System.Text.RegularExpressions;

namespace Pancras.Web.Tests;

/// <summary>
/// Lỗi UAT: ô "Thuế khác (VNĐ)" hiện 200000 thay vì 200.000 vì thiếu hook định dạng tiền.
/// Luật (docs/03-ui-ux/formatting-and-copy.md mục 1): mọi ô nhập tiền VNĐ có thuộc tính <c>data-vnd</c>.
/// </summary>
public class MoneyInputsTests
{
    private static readonly string PagesRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../src/Pancras.Web/Pages"));

    // Tên trường là tiền VNĐ. Tiền USD, phần trăm, số lượng không nằm trong danh sách này.
    private static readonly Regex MoneyField = new(
        @"(CostPrice|RetailPrice|DealerPrice|DistributorPrice|ProjectPrice|SupplementalDuty|ExchangeRate|Charges\[@i\]\.Amount)\b|\(VNĐ[,)]",
        RegexOptions.Compiled);

    [Fact]
    public void EveryVndInput_HasTheDataVndHook()
    {
        var offenders = new List<string>();
        foreach (var file in Directory.EnumerateFiles(PagesRoot, "*.cshtml", SearchOption.AllDirectories))
        {
            var html = File.ReadAllText(file);
            foreach (Match tag in Regex.Matches(html, @"<input\b[^>]*>"))
            {
                if (!MoneyField.IsMatch(tag.Value)
                    || tag.Value.Contains("data-vnd", StringComparison.Ordinal)
                    || tag.Value.Contains("type=\"hidden\"", StringComparison.Ordinal)
                    || tag.Value.Contains(" disabled", StringComparison.Ordinal))
                {
                    continue;
                }

                offenders.Add(Path.GetRelativePath(PagesRoot, file) + ": " + tag.Value[..Math.Min(120, tag.Value.Length)]);
            }
        }

        Assert.True(offenders.Count == 0, "Ô tiền VNĐ thiếu data-vnd:\n" + string.Join("\n", offenders));
    }

    [Fact]
    public void OneSharedScript_FormatsMoneyInputs_OnEveryPage()
    {
        // Trước đây Danh mục và Mua hàng mỗi trang một cách định dạng ô tiền. Nay chỉ vnd-input.js định dạng; script khác gọi window.PancrasVnd.
        var web = Path.GetFullPath(Path.Combine(PagesRoot, ".."));
        Assert.Contains("vnd-input.js", File.ReadAllText(Path.Combine(PagesRoot, "Shared/_Head.cshtml")), StringComparison.Ordinal);

        var offenders = Directory.EnumerateFiles(Path.Combine(web, "wwwroot/js"), "*.js")
            .Where(file => Path.GetFileName(file) != "vnd-input.js")
            .Where(file => File.ReadAllText(file).Contains("querySelectorAll(\"[data-vnd]\")", StringComparison.Ordinal))
            .Select(Path.GetFileName)
            .ToArray();

        Assert.True(offenders.Length == 0, "Định dạng ô tiền bằng window.PancrasVnd: " + string.Join(", ", offenders));
    }

    [Fact]
    public void NoView_UsesTheOldVndClassHook()
    {
        var offenders = Directory.EnumerateFiles(PagesRoot, "*.cshtml", SearchOption.AllDirectories)
            .Where(file => Regex.IsMatch(File.ReadAllText(file), @"class=""[^""]*\bvnd\b"))
            .Select(file => Path.GetRelativePath(PagesRoot, file))
            .ToArray();

        Assert.True(offenders.Length == 0, "Dùng data-vnd, không dùng class vnd: " + string.Join(", ", offenders));
    }
}
