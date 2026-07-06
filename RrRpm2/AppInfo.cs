using System.Diagnostics;
using System.Reflection;

namespace RrRpm2;

internal static class AppInfo
{
    public const string Name = "RadioReference to RPM2";
    public const string RepositoryUrl = "https://github.com/Brantlab/RR-to-RPM2";
    public const string UserGuideUrl = RepositoryUrl + "/blob/main/howtouse.md";

    public static string DisplayVersion
    {
        get
        {
            var informationalVersion = Assembly.GetExecutingAssembly()
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                ?.InformationalVersion;

            return informationalVersion?.Split('+')[0]
                ?? Assembly.GetExecutingAssembly().GetName().Version?.ToString(3)
                ?? "Unknown";
        }
    }

    public static Version Version
    {
        get
        {
            var numericVersion = DisplayVersion.Split('-')[0];
            return System.Version.TryParse(numericVersion, out var version)
                ? version
                : new Version(0, 0, 0);
        }
    }

    public static void OpenUrl(string url)
    {
        Process.Start(new ProcessStartInfo(url)
        {
            UseShellExecute = true
        });
    }
}
