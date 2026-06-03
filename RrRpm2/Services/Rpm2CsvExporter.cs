using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using RrRpm2.Models;

namespace RrRpm2.Services;

public sealed record Rpm2ExportOptions(
    string GroupSetName,
    string SystemId,
    string VisualColor,
    string LongNameSource,
    bool ConventionalFallbackByChannel,
    bool UseGroupIdZero,
    bool SuppressKeyNumber,
    bool EncryptCallParameters,
    bool Transmit,
    bool Receive,
    bool Calls,
    bool Scan,
    bool AlertTones,
    bool Backlight,
    bool ScanListMember,
    bool Mandown);

public static class Rpm2CsvExporter
{
    private static readonly string[] Header =
    [
        "Name",
        "Type",
        "Emergency/Home Group",
        "Priority 1 Group",
        "Priority 2 Group",
        "Dynamic Group",
        "Priority Talk Group",
        "Multi Group",
        "System ID (Hex/Base 10)",
        "Announcement Group ID",
        "Use Conv Emer/Home System",
        "Group Name",
        "Group ID",
        "Voice Annunciation",
        "Visual Group Identification",
        "Conventional Fallback by Channel",
        "Trunked Frequency Set",
        "Trunked Frequency Channel",
        "Use Group ID 0",
        "Long Name",
        "Encrypted",
        "Encrypted Type",
        "Key Number",
        "Suppress Key Number",
        "Encrypt Call Parameters",
        "Transmit",
        "Receive",
        "Calls",
        "Scan",
        "Alert Tones",
        "Backlight",
        "Scan List Member",
        "Subgroup of MG",
        "Channel Strapping",
        "Mandown",
        "Channel Index"
    ];

    public static void Write(string path, IReadOnlyList<Talkgroup> talkgroups, Rpm2ExportOptions options)
    {
        using var writer = new StreamWriter(path, false, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        writer.WriteLine(ToCsv(Header));
        writer.WriteLine(ToCsv(GroupSetRow(options)));

        var channelIndex = 1;
        foreach (var talkgroup in talkgroups)
        {
            writer.WriteLine(ToCsv(TalkgroupRow(talkgroup, options, channelIndex)));
            channelIndex++;
        }
    }

    public static string CleanName(string value, int maxLength)
    {
        var cleaned = new string(value
            .Where(c => char.IsLetterOrDigit(c) || char.IsWhiteSpace(c) || c is '-' or '_' or '/')
            .ToArray())
            .Trim();

        if (string.IsNullOrWhiteSpace(cleaned))
        {
            cleaned = "RR IMPORT";
        }

        return cleaned.Length <= maxLength ? cleaned : cleaned[..maxLength].Trim();
    }

    private static string[] GroupSetRow(Rpm2ExportOptions options)
    {
        var row = EmptyRow();
        row[0] = options.GroupSetName;
        row[1] = "P25 Group Set";
        row[8] = options.SystemId;
        row[9] = "0";
        row[10] = "False";
        return row;
    }

    private static string[] TalkgroupRow(Talkgroup talkgroup, Rpm2ExportOptions options, int channelIndex)
    {
        var row = EmptyRow();
        row[0] = options.GroupSetName;
        row[11] = BuildGroupName(talkgroup);
        row[12] = talkgroup.DecimalId.ToString(CultureInfo.InvariantCulture);
        row[14] = options.VisualColor;
        row[15] = ToRpmBool(options.ConventionalFallbackByChannel);
        row[18] = ToRpmBool(options.UseGroupIdZero);
        row[19] = BuildLongName(talkgroup, options);
        row[20] = talkgroup.IsEncrypted ? "TRUE" : "FALSE";
        row[21] = "Use System";
        row[22] = "0";
        row[23] = ToRpmBool(options.SuppressKeyNumber);
        row[24] = ToRpmBool(options.EncryptCallParameters);
        row[25] = ToRpmBool(options.Transmit);
        row[26] = ToRpmBool(options.Receive);
        row[27] = ToRpmBool(options.Calls);
        row[28] = ToRpmBool(options.Scan);
        row[29] = ToRpmBool(options.AlertTones);
        row[30] = ToRpmBool(options.Backlight);
        row[31] = ToRpmBool(options.ScanListMember);
        row[32] = "FALSE";
        row[33] = "Follow Digital Voice Options";
        row[34] = ToRpmBool(options.Mandown);
        row[35] = channelIndex.ToString("0000", CultureInfo.InvariantCulture);
        return row;
    }

    private static string BuildGroupName(Talkgroup talkgroup)
    {
        var source = string.IsNullOrWhiteSpace(talkgroup.Alpha)
            ? talkgroup.Description
            : talkgroup.Alpha;

        return CleanName(source.Replace(" ", string.Empty).ToUpperInvariant(), 8);
    }

    private static string BuildLongName(Talkgroup talkgroup, Rpm2ExportOptions options)
    {
        var source = options.LongNameSource switch
        {
            "Alpha" => talkgroup.Alpha,
            "Description" => talkgroup.Description,
            "Category + Alpha" => $"{talkgroup.CategoryName} {talkgroup.Alpha}",
            _ => BuildSmartLongName(talkgroup)
        };

        return CleanName(source.ToUpperInvariant(), 16);
    }

    private static string BuildSmartLongName(Talkgroup talkgroup)
    {
        var alpha = NormalizeNamePart(talkgroup.Alpha);
        var description = NormalizeNamePart(talkgroup.Description);
        var category = NormalizeNamePart(talkgroup.CategoryName);

        if (!string.IsNullOrWhiteSpace(alpha) && alpha.Length <= 16)
        {
            return alpha;
        }

        var shortCategory = AbbreviateNamePart(category);
        var shortAlpha = AbbreviateNamePart(alpha);
        var combined = $"{shortCategory} {shortAlpha}".Trim();
        if (!string.IsNullOrWhiteSpace(combined) && combined.Length <= 16)
        {
            return combined;
        }

        var shortDescription = AbbreviateNamePart(description);
        combined = $"{shortCategory} {shortDescription}".Trim();
        if (!string.IsNullOrWhiteSpace(combined) && combined.Length <= 16)
        {
            return combined;
        }

        return !string.IsNullOrWhiteSpace(shortAlpha)
            ? shortAlpha
            : shortDescription;
    }

    private static string NormalizeNamePart(string value)
    {
        return value
            .Replace(" Law Dispatch", " Law", StringComparison.OrdinalIgnoreCase)
            .Replace(" Fire Dispatch", " Fire", StringComparison.OrdinalIgnoreCase)
            .Replace(" Police Dispatch", " Police", StringComparison.OrdinalIgnoreCase)
            .Replace(" Sheriff Dispatch", " Sheriff", StringComparison.OrdinalIgnoreCase)
            .Trim();
    }

    private static string AbbreviateNamePart(string value)
    {
        return NormalizeNamePart(value)
            .Replace("County", "CO", StringComparison.OrdinalIgnoreCase)
            .Replace("Dispatch", "DISP", StringComparison.OrdinalIgnoreCase)
            .Replace("Tactical", "TAC", StringComparison.OrdinalIgnoreCase)
            .Replace("Operations", "OPS", StringComparison.OrdinalIgnoreCase)
            .Replace("Emergency Medical Services", "EMS", StringComparison.OrdinalIgnoreCase)
            .Replace("Emergency Management", "EMA", StringComparison.OrdinalIgnoreCase)
            .Replace("Sheriff", "SO", StringComparison.OrdinalIgnoreCase)
            .Replace("Police", "PD", StringComparison.OrdinalIgnoreCase)
            .Replace("Fire", "FD", StringComparison.OrdinalIgnoreCase)
            .Replace("Department", "DEPT", StringComparison.OrdinalIgnoreCase)
            .Replace("Hospital", "HOSP", StringComparison.OrdinalIgnoreCase)
            .Replace("Health", "HLTH", StringComparison.OrdinalIgnoreCase)
            .Replace("Countywide", "COWIDE", StringComparison.OrdinalIgnoreCase)
            .Trim();
    }

    private static string[] EmptyRow()
    {
        return Enumerable.Repeat(string.Empty, Header.Length).ToArray();
    }

    private static string ToRpmBool(bool value)
    {
        return value ? "TRUE" : "FALSE";
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
