using System.Security;
using HitAScreen.Platform.Abstractions;

namespace HitAScreen.Platform.MacOS;

public sealed class MacLaunchAtLoginService : ILaunchAtLoginService
{
    private const string LaunchAgentLabel = "com.hitascreen.agent";

    public bool IsEnabled()
    {
        if (!OperatingSystem.IsMacOS())
        {
            return false;
        }

        return File.Exists(GetLaunchAgentPath());
    }

    public bool SetEnabled(bool enabled, out string? errorMessage)
    {
        if (!OperatingSystem.IsMacOS())
        {
            errorMessage = "macOS のみ対応しています。";
            return false;
        }

        try
        {
            var launchAgentPath = GetLaunchAgentPath();
            var directory = Path.GetDirectoryName(launchAgentPath);
            if (string.IsNullOrWhiteSpace(directory))
            {
                errorMessage = "LaunchAgents ディレクトリの解決に失敗しました。";
                return false;
            }

            Directory.CreateDirectory(directory);

            if (enabled)
            {
                var executable = Environment.ProcessPath;
                if (string.IsNullOrWhiteSpace(executable) || !File.Exists(executable))
                {
                    errorMessage = "実行ファイルパスを取得できませんでした。";
                    return false;
                }

                File.WriteAllText(launchAgentPath, BuildLaunchAgentPlist(executable));
            }
            else if (File.Exists(launchAgentPath))
            {
                File.Delete(launchAgentPath);
            }

            errorMessage = null;
            return true;
        }
        catch (Exception ex)
        {
            errorMessage = $"自動起動設定の更新に失敗しました: {ex.Message}";
            return false;
        }
    }

    private static string GetLaunchAgentPath()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return Path.Combine(home, "Library", "LaunchAgents", LaunchAgentLabel + ".plist");
    }

    private static string BuildLaunchAgentPlist(string executable)
    {
        var escapedPath = SecurityElement.Escape(executable) ?? executable;
        return string.Join(
            Environment.NewLine,
            "<?xml version=\"1.0\" encoding=\"UTF-8\"?>",
            "<!DOCTYPE plist PUBLIC \"-//Apple//DTD PLIST 1.0//EN\" \"http://www.apple.com/DTDs/PropertyList-1.0.dtd\">",
            "<plist version=\"1.0\">",
            "<dict>",
            "  <key>Label</key>",
            $"  <string>{LaunchAgentLabel}</string>",
            "  <key>ProgramArguments</key>",
            "  <array>",
            $"    <string>{escapedPath}</string>",
            "  </array>",
            "  <key>RunAtLoad</key>",
            "  <true/>",
            "</dict>",
            "</plist>");
    }
}
