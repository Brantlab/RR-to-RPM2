namespace RrRpm2.Models;

public sealed class TelemetryPreferences
{
    public bool? Enabled { get; set; }

    public Guid? InstallationId { get; set; }

    public DateTimeOffset? LastDailyActiveUtc { get; set; }
}
