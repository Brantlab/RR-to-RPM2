using System.IO;
using System.Text.Json;
using RrRpm2.Models;

namespace RrRpm2.Services;

internal static class TelemetryPreferenceStorage
{
    private static readonly string PreferencePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "RR-RPM2",
        "telemetry.json");

    public static TelemetryPreferences Load()
    {
        if (!File.Exists(PreferencePath))
        {
            return new TelemetryPreferences();
        }

        try
        {
            var json = File.ReadAllText(PreferencePath);
            return JsonSerializer.Deserialize<TelemetryPreferences>(json)
                ?? new TelemetryPreferences();
        }
        catch (Exception ex)
        {
            App.LogException(ex);
            return new TelemetryPreferences();
        }
    }

    public static void Save(TelemetryPreferences preferences)
    {
        var directory = Path.GetDirectoryName(PreferencePath)!;
        Directory.CreateDirectory(directory);

        var temporaryPath = PreferencePath + ".tmp";
        var json = JsonSerializer.Serialize(preferences);
        File.WriteAllText(temporaryPath, json);
        File.Move(temporaryPath, PreferencePath, true);
    }
}
