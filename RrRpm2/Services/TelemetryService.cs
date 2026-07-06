using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Runtime.InteropServices;
using RrRpm2.Models;

namespace RrRpm2.Services;

public sealed class TelemetryService
{
    private static readonly Uri EventEndpoint =
        new("https://telemetry.brantlab.xyz/v1/events");

    private static readonly HttpClient HttpClient = CreateHttpClient();
    private readonly SemaphoreSlim _preferenceLock = new(1, 1);
    private readonly TelemetryPreferences _preferences = TelemetryPreferenceStorage.Load();

    public bool HasConsentDecision => _preferences.Enabled.HasValue;

    public bool IsEnabled => _preferences.Enabled == true;

    public void SetEnabled(bool enabled)
    {
        var wasEnabled = IsEnabled;
        _preferences.Enabled = enabled;

        if (enabled)
        {
            _preferences.InstallationId ??= Guid.NewGuid();
        }
        else
        {
            _preferences.InstallationId = null;
            _preferences.LastDailyActiveUtc = null;
        }

        TelemetryPreferenceStorage.Save(_preferences);

        if (enabled && !wasEnabled)
        {
            _ = TrackAsync("telemetry_enabled");
        }
    }

    public async Task TrackStartupAsync()
    {
        if (!IsEnabled)
        {
            return;
        }

        await TrackAsync("app_started").ConfigureAwait(false);

        var today = DateTimeOffset.UtcNow.Date;
        if (_preferences.LastDailyActiveUtc?.UtcDateTime.Date == today)
        {
            return;
        }

        if (await SendAsync("daily_active", null).ConfigureAwait(false))
        {
            await _preferenceLock.WaitAsync().ConfigureAwait(false);
            try
            {
                if (IsEnabled)
                {
                    _preferences.LastDailyActiveUtc = DateTimeOffset.UtcNow;
                    TelemetryPreferenceStorage.Save(_preferences);
                }
            }
            catch (Exception ex)
            {
                App.LogException(ex);
            }
            finally
            {
                _preferenceLock.Release();
            }
        }
    }

    public Task TrackUpdateCheckAsync(string result)
    {
        return TrackAsync(
            "update_checked",
            new Dictionary<string, string> { ["result"] = result });
    }

    public Task TrackExportAsync(string eventName, int count)
    {
        return TrackAsync(
            eventName,
            new Dictionary<string, string> { ["count_bucket"] = CountBucket(count) });
    }

    public Task TrackCrashAsync(Exception exception)
    {
        return TrackAsync(
            "app_crashed",
            new Dictionary<string, string>
            {
                ["exception_type"] = exception.GetType().FullName
                    ?? exception.GetType().Name
            });
    }

    public async Task TrackAsync(
        string eventName,
        Dictionary<string, string>? properties = null)
    {
        if (!IsEnabled)
        {
            return;
        }

        await SendAsync(eventName, properties).ConfigureAwait(false);
    }

    private async Task<bool> SendAsync(
        string eventName,
        Dictionary<string, string>? properties)
    {
        var installationId = _preferences.InstallationId;
        if (!IsEnabled || installationId is null)
        {
            return false;
        }

        var payload = new
        {
            eventId = Guid.NewGuid().ToString("D"),
            eventName,
            installationId = installationId.Value.ToString("D"),
            appVersion = AppInfo.DisplayVersion,
            osVersion = Environment.OSVersion.VersionString,
            architecture = RuntimeInformation.OSArchitecture.ToString(),
            properties = properties ?? []
        };

        try
        {
            using var response = await HttpClient.PostAsJsonAsync(
                EventEndpoint,
                payload).ConfigureAwait(false);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return false;
        }
    }

    private static string CountBucket(int count)
    {
        return count switch
        {
            <= 1 => "1",
            <= 10 => "2-10",
            <= 50 => "11-50",
            <= 200 => "51-200",
            _ => "201+"
        };
    }

    private static HttpClient CreateHttpClient()
    {
        var client = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(5)
        };
        client.DefaultRequestHeaders.UserAgent.Add(
            new ProductInfoHeaderValue("RR-to-RPM2", AppInfo.DisplayVersion));
        return client;
    }
}
