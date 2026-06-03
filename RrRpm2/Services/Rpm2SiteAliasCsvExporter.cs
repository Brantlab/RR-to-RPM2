using System.Globalization;
using System.IO;
using System.Text;
using RrRpm2.Models;

namespace RrRpm2.Services;

public static class Rpm2SiteAliasCsvExporter
{
    private static readonly string[] Header =
    [
        "Name",
        "Type",
        "P25 WAN List",
        "Site Name",
        "WA Network",
        "SITE ID",
        "RFSS ID"
    ];

    public static void Write(string path, IReadOnlyList<TrunkedSite> sites, Rpm2SiteAliasOptions options)
    {
        using var writer = new StreamWriter(path, false, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        writer.WriteLine(ToCsv(Header));
        writer.WriteLine(ToCsv([
            options.Name,
            "Site Alias",
            options.WanList,
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty
        ]));

        var usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var site in sites.OrderBy(s => SiteSortValue(s)).ThenBy(s => s.Description))
        {
            var siteName = SiteName(site, usedNames);
            writer.WriteLine(ToCsv([
                options.Name,
                string.Empty,
                string.Empty,
                siteName,
                options.Network,
                HexValue(SiteAliasId(site)),
                HexValue(RfssAliasId(site))
            ]));
        }
    }

    private static int SiteSortValue(TrunkedSite site)
    {
        return TryReadSiteNumber(site, out var value) ? value : site.SiteId;
    }

    private static string SiteName(TrunkedSite site, HashSet<string> usedNames)
    {
        var baseName = CleanAliasName(site.Description);
        if (baseName.Length == 0)
        {
            baseName = CleanAliasName(site.Location);
        }

        if (baseName.Length == 0)
        {
            baseName = NumberedName("SITE", SiteAliasId(site));
        }

        if (usedNames.Add(baseName))
        {
            return baseName;
        }

        var siteId = SiteAliasId(site);
        for (var suffixLength = 1; suffixLength <= 4; suffixLength++)
        {
            var suffix = Math.Max(siteId, 0).ToString(new string('0', suffixLength), CultureInfo.InvariantCulture);
            if (suffix.Length > suffixLength)
            {
                suffix = suffix[^suffixLength..];
            }

            var prefixLength = Math.Max(1, 8 - suffix.Length);
            var candidate = $"{baseName[..Math.Min(baseName.Length, prefixLength)]}{suffix}";
            if (usedNames.Add(candidate))
            {
                return candidate;
            }
        }

        var counter = 1;
        while (true)
        {
            var suffix = counter.ToString("000", CultureInfo.InvariantCulture);
            var candidate = $"{baseName[..Math.Min(baseName.Length, 5)]}{suffix}";
            if (usedNames.Add(candidate))
            {
                return candidate;
            }

            counter++;
        }
    }

    private static int SiteAliasId(TrunkedSite site)
    {
        if (site.RfssSiteId is > 0)
        {
            return site.RfssSiteId.Value;
        }

        var numbers = ReadSiteNumberParts(site);
        if (numbers.Count >= 2)
        {
            return numbers[^1];
        }

        return numbers.Count == 1 ? numbers[0] : site.SiteId;
    }

    private static int RfssAliasId(TrunkedSite site)
    {
        if (site.RfssId is > 0)
        {
            return site.RfssId.Value;
        }

        var numbers = ReadSiteNumberParts(site);
        return numbers.Count >= 2 ? numbers[0] : 0;
    }

    private static bool TryReadSiteNumber(TrunkedSite site, out int value)
    {
        var numbers = ReadSiteNumberParts(site);
        value = numbers.Count > 0 ? numbers[^1] : 0;
        return value > 0;
    }

    private static List<int> ReadSiteNumberParts(TrunkedSite site)
    {
        var parts = site.SiteNumber
            .Split(['-', '.', '/', ':', ' ', '(', ')', '[', ']'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(ParseSiteNumberPart)
            .Where(x => x > 0)
            .ToList();

        if (parts.Count > 0)
        {
            return parts;
        }

        var digits = new string(site.SiteNumber.Where(char.IsDigit).ToArray());
        return int.TryParse(digits, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) && value > 0
            ? [value]
            : [];
    }

    private static int ParseSiteNumberPart(string value)
    {
        if (value.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            return int.TryParse(value[2..], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var hexValue)
                ? hexValue
                : 0;
        }

        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var decimalValue)
            ? decimalValue
            : 0;
    }

    private static string HexValue(int value)
    {
        return $"0x{Math.Max(value, 0).ToString("X", CultureInfo.InvariantCulture)}";
    }

    private static string NumberedName(string prefix, int value)
    {
        var cleanPrefix = CleanAliasName(prefix);
        if (cleanPrefix.Length == 0)
        {
            cleanPrefix = "SITE";
        }

        var suffix = Math.Max(value, 0).ToString("0000", CultureInfo.InvariantCulture);
        if (suffix.Length >= 8)
        {
            return suffix[^8..];
        }

        var prefixLength = 8 - suffix.Length;
        return $"{cleanPrefix[..Math.Min(cleanPrefix.Length, prefixLength)]}{suffix}";
    }

    public static string CleanAliasName(string value)
    {
        var builder = new StringBuilder(8);
        foreach (var character in value.Where(char.IsLetterOrDigit))
        {
            builder.Append(char.ToUpperInvariant(character));
            if (builder.Length == 8)
            {
                break;
            }
        }

        return builder.ToString();
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

public sealed record Rpm2SiteAliasOptions(
    string Name,
    string WanList,
    string Network);
