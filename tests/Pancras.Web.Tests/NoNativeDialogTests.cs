using System.Text.RegularExpressions;

namespace Pancras.Web.Tests;

public class NoNativeDialogTests
{
    private static readonly Regex NativeDialog = new(@"(?<![\w.])(confirm|alert|prompt)\s*\(", RegexOptions.Compiled);

    [Fact]
    public void Razor_pages_do_not_use_native_browser_dialogs()
    {
        var pagesDirectory = FindPagesDirectory();
        var offenders = new List<string>();

        foreach (var file in Directory.EnumerateFiles(pagesDirectory, "*.cshtml", SearchOption.AllDirectories))
        {
            var lines = File.ReadAllLines(file);
            for (var i = 0; i < lines.Length; i++)
            {
                if (NativeDialog.IsMatch(lines[i]))
                {
                    offenders.Add($"{Path.GetRelativePath(pagesDirectory, file)}:{i + 1}");
                }
            }
        }

        Assert.True(
            offenders.Count == 0,
            "Use the <dialog> confirm (data-confirm, partial _ConfirmDialog) instead of confirm()/alert()/prompt(): "
            + string.Join(", ", offenders));
    }

    private static string FindPagesDirectory()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "src", "Pancras.Web", "Pages");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Cannot locate src/Pancras.Web/Pages from the test output directory.");
    }
}
