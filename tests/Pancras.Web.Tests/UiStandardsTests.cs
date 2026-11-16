using System.Text.RegularExpressions;

namespace Pancras.Web.Tests;

/// <summary>Giữ các chuẩn giao diện trong docs/03-ui-ux/components.md khỏi bị lệch dần: quét thẳng các file .cshtml.</summary>
public class UiStandardsTests
{
    private static readonly string PagesDirectory = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../src/Pancras.Web/Pages"));

    private static IEnumerable<(string File, string Text)> Views() =>
        Directory.EnumerateFiles(PagesDirectory, "*.cshtml", SearchOption.AllDirectories)
            .Select(file => (Path.GetRelativePath(PagesDirectory, file), File.ReadAllText(file)));

    [Fact]
    public void SolidButtons_UseTheStandardSizeWeightAndHover()
    {
        var offenders = new List<string>();
        foreach (var (file, text) in Views())
        {
            foreach (Match match in Regex.Matches(text, "<(?:button|a)\b[^>]*?\bclass=\"([^\"]*)\""))
            {
                var classes = match.Groups[1].Value.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (classes.Any(c => c.Contains('@')) || classes.Contains("rounded-full") || !classes.Contains("text-white"))
                {
                    continue;
                }

                var solid = classes.Any(c => c is "bg-sky-600" or "bg-rose-600" or "bg-emerald-600" or "bg-violet-600");
                if (!solid || !classes.Any(c => c.StartsWith("px-", StringComparison.Ordinal)))
                {
                    continue;
                }

                var standard = classes.Contains("rounded-lg") && classes.Contains("font-bold")
                    && classes.Any(c => c.StartsWith("hover:bg-", StringComparison.Ordinal))
                    && (classes.Contains("px-4") && classes.Contains("py-2") && classes.Contains("text-sm")
                        || classes.Contains("px-3") && classes.Contains("py-1.5") && classes.Contains("text-xs"));
                if (!standard)
                {
                    offenders.Add(file + ": " + match.Groups[1].Value);
                }
            }
        }

        Assert.Empty(offenders);
    }

    [Fact]
    public void Dialogs_UseTheAllowedWidths_AndAFlexColumnFrame()
    {
        var allowed = new[] { "28rem", "40rem", "56rem", "72rem" };
        var offenders = new List<string>();
        foreach (var (file, text) in Views())
        {
            foreach (Match match in Regex.Matches(text, "<dialog\b[^>]*?\bclass=\"([^\"]*)\""))
            {
                var classes = match.Groups[1].Value;
                if (classes.Contains('@'))
                {
                    continue;
                }

                var width = Regex.Match(classes, @"w-[min(([d.]+rem),").Groups[1].Value;
                if (!allowed.Contains(width) || !classes.Contains("flex-col") || classes.Contains("max-h-[90vh]"))
                {
                    offenders.Add(file + ": " + classes);
                }
            }
        }

        Assert.Empty(offenders);
    }

    [Fact]
    public void Tables_HaveAShadedHeader_AndPaddedCells()
    {
        var offenders = new List<string>();
        foreach (var (file, text) in Views())
        {
            foreach (Match match in Regex.Matches(text, "<thead class=\"([^\"]*)\""))
            {
                if (!match.Groups[1].Value.Contains("bg-slate-50") && !match.Groups[1].Value.Contains('@'))
                {
                    offenders.Add(file + ": thead " + match.Groups[1].Value);
                }
            }

            foreach (Match match in Regex.Matches(text, "<t[dh]\b[^>]*?\bclass=\"([^\"]*)\""))
            {
                var classes = match.Groups[1].Value.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (classes.Contains("px-4") || (classes.Any(c => c.StartsWith("pr-", StringComparison.Ordinal)) && !classes.Any(c => c.StartsWith("px-", StringComparison.Ordinal))))
                {
                    offenders.Add(file + ": cell " + match.Groups[1].Value);
                }
            }
        }

        Assert.Empty(offenders);
    }

    [Fact]
    public void PageTitles_UseTheStandardHeading()
    {
        var offenders = Views()
            .Where(view => view.Text.Contains("<h1", StringComparison.Ordinal))
            .Where(view => !view.Text.Contains("font-display text-xl sm:text-2xl font-bold text-slate-900", StringComparison.Ordinal))
            .Select(view => view.File)
            .ToArray();

        Assert.Empty(offenders);
    }

    [Fact]
    public void DismissButtons_SayQuayLai_NotHuy()
    {
        var offenders = Views()
            .Where(view => Regex.IsMatch(view.Text, @">Hủy( bỏ)?<"))
            .Select(view => view.File)
            .ToArray();

        Assert.Empty(offenders);
    }
}
