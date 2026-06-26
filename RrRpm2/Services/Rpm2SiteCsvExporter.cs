using System.Globalization;
using System.IO;
using System.Text;
using RrRpm2.Models;

namespace RrRpm2.Services;

public static class Rpm2SiteCsvExporter
{
    private static readonly string[] Header =
    [
        "Name",
        "Type",
        "Bandsplit (MHz)",
        "CC Limits",
        "EDACS Bandwidth",
        "Signaling Baud Rate",
        "Transmit Frequency (MHz)",
        "Receive Frequency (MHz)",
        "NPSPAC",
        "Channel Index"
    ];

    public static int Write(
        string path,
        IReadOnlyList<TrunkedSite> sites,
        string name,
        decimal transmitPlaceholder,
        Rpm2SiteFrequencyExportOptions options)
    {
        using var writer = new StreamWriter(path, false, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        writer.WriteLine(ToCsv(Header));
        writer.WriteLine(ToCsv(FrequencySetRow(name)));

        var channelIndex = 1;
        foreach (var frequency in sites
            .SelectMany(s => s.Frequencies)
            .Where(options.Includes)
            .DistinctBy(f => f.Frequency)
            .OrderBy(f => f.Frequency))
        {
            writer.WriteLine(ToCsv(FrequencyRow(name, ResolveTransmitFrequency(frequency.Frequency, transmitPlaceholder), frequency.Frequency, channelIndex)));
            channelIndex++;
        }

        return channelIndex - 1;
    }

    private static string[] FrequencySetRow(string name)
    {
        return
        [
            name,
            "Trunked Frequency Set",
            "136.00000 - 941.00000",
            "All Channels",
            "Wide",
            "9600",
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty
        ];
    }

    private static string[] FrequencyRow(string name, decimal transmitFrequency, decimal receiveFrequency, int channelIndex)
    {
        var transmitValue = transmitFrequency.ToString("0.00000", CultureInfo.InvariantCulture);
        var receiveValue = receiveFrequency.ToString("0.00000", CultureInfo.InvariantCulture);
        return
        [
            name,
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            transmitValue,
            receiveValue,
            IsNpspac(receiveFrequency) ? "TRUE" : "FALSE",
            channelIndex.ToString("0000", CultureInfo.InvariantCulture)
        ];
    }

    private static bool IsNpspac(decimal frequency)
    {
        return frequency is >= 866.00000m and <= 869.00000m;
    }

    private static decimal ResolveTransmitFrequency(decimal receiveFrequency, decimal fallbackTransmitFrequency)
    {
        var receiveBank = FindTransmitBank(receiveFrequency);
        if (receiveBank is null)
        {
            return fallbackTransmitFrequency;
        }

        return IsInRange(fallbackTransmitFrequency, receiveBank.Value.Start, receiveBank.Value.End)
            ? fallbackTransmitFrequency
            : receiveBank.Value.Start;
    }

    private static (decimal Start, decimal End)? FindTransmitBank(decimal frequency)
    {
        if (IsInRange(frequency, 136.00000m, 174.00000m))
        {
            return (136.00000m, 174.00000m);
        }

        if (IsInRange(frequency, 380.00000m, 512.00000m))
        {
            return (380.00000m, 512.00000m);
        }

        if (IsInRange(frequency, 763.00000m, 776.00000m))
        {
            return (763.00000m, 776.00000m);
        }

        if (IsInRange(frequency, 793.00000m, 805.99375m))
        {
            return (793.00000m, 805.99375m);
        }

        if (IsInRange(frequency, 825.00000m, 896.00000m))
        {
            return (825.00000m, 896.00000m);
        }

        return null;
    }

    private static bool IsInRange(decimal value, decimal start, decimal end)
    {
        return value >= start && value <= end;
    }

    private static string ToCsv(IEnumerable<string> values)
    {
        return string.Join(",", values.Select(Escape));
    }

    private static string Escape(string value)
    {
        if (value.Contains('"') || value.Contains(',') || value.Contains('\r') || value.Contains('\n'))
        {
            return $"\"{value.Replace("\"", "\"\"")}\"";
        }

        return value;
    }
}

public sealed record Rpm2SiteFrequencyExportOptions(
    bool IncludeDedicatedControl,
    bool IncludeAlternateControl,
    bool IncludeGenericControl,
    bool IncludeOtherFrequencies)
{
    public bool Includes(TrunkedSiteFrequency frequency)
    {
        return frequency.UseCategory switch
        {
            TrunkedSiteFrequencyUse.DedicatedControl => IncludeDedicatedControl,
            TrunkedSiteFrequencyUse.AlternateControl => IncludeAlternateControl,
            TrunkedSiteFrequencyUse.GenericControl => IncludeGenericControl,
            _ => IncludeOtherFrequencies
        };
    }
}
