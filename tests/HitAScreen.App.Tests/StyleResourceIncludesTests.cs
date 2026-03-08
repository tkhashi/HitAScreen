using Xunit;

namespace HitAScreen.App.Tests;

public sealed class StyleResourceIncludesTests
{
    [Fact]
    public void AppAxaml_ShouldIncludeStyleDictionaries()
    {
        var repoRoot = FindRepoRoot();
        var appAxaml = Path.Combine(repoRoot, "src", "HitAScreen.App", "App.axaml");
        var content = File.ReadAllText(appAxaml);

        Assert.Contains("Styles/Tokens.axaml", content, StringComparison.Ordinal);
        Assert.Contains("Styles/Controls.axaml", content, StringComparison.Ordinal);
        Assert.Contains("Styles/MainWindowStyles.axaml", content, StringComparison.Ordinal);
        Assert.Contains("Styles/OverlayStyles.axaml", content, StringComparison.Ordinal);
    }

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "HitAScreen.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("リポジトリルートを特定できませんでした。");
    }
}
