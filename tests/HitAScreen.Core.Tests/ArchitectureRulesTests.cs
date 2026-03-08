using System.Text.RegularExpressions;
using Xunit;

namespace HitAScreen.Core.Tests;

public sealed class ArchitectureRulesTests
{
    [Fact]
    public void InfrastructureProject_ShouldNotReferenceCoreProject()
    {
        var repoRoot = FindRepoRoot();
        var infrastructureProject = Path.Combine(repoRoot, "src", "HitAScreen.Infrastructure", "HitAScreen.Infrastructure.csproj");
        var content = File.ReadAllText(infrastructureProject);

        Assert.DoesNotContain("HitAScreen.Core.csproj", content, StringComparison.Ordinal);
    }

    [Fact]
    public void Abstractions_ShouldNotContainSettingsNormalizer()
    {
        var repoRoot = FindRepoRoot();
        var abstractionsDir = Path.Combine(repoRoot, "src", "HitAScreen.Platform.Abstractions");
        var files = Directory.GetFiles(abstractionsDir, "*.cs", SearchOption.AllDirectories);

        foreach (var file in files)
        {
            var content = File.ReadAllText(file);
            Assert.DoesNotContain("class UserSettingsNormalizer", content, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void MainWindowCodeBehind_ShouldNotContainFixedRecentColorStyleValues()
    {
        var repoRoot = FindRepoRoot();
        var mainWindowCodeBehind = Path.Combine(repoRoot, "src", "HitAScreen.App", "Views", "MainWindow.axaml.cs");
        var content = File.ReadAllText(mainWindowCodeBehind);

        Assert.DoesNotMatch(new Regex(@"Width\s*=\s*24", RegexOptions.CultureInvariant), content);
        Assert.DoesNotMatch(new Regex(@"Height\s*=\s*24", RegexOptions.CultureInvariant), content);
        Assert.DoesNotMatch(new Regex(@"Margin\s*=\s*new\s+Thickness\(0,\s*0,\s*6,\s*6\)", RegexOptions.CultureInvariant), content);
        Assert.Contains("UI-02 例外", content, StringComparison.Ordinal);
    }

    [Fact]
    public void OverlayWindowCodeBehind_ShouldDeclareUiExceptionForDynamicStyles()
    {
        var repoRoot = FindRepoRoot();
        var overlayCodeBehind = Path.Combine(repoRoot, "src", "HitAScreen.App", "Views", "OverlayWindow.axaml.cs");
        var content = File.ReadAllText(overlayCodeBehind);

        Assert.Contains("UI-02 例外", content, StringComparison.Ordinal);
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
