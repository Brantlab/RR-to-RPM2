using System.Globalization;
using System.Net.Http;
using System.Text;
using System.Xml.Linq;
using RrRpm2.Models;

namespace RrRpm2.Services;

public sealed class RadioReferenceClient
{
    private const string Namespace = "http://api.radioreference.com/soap2";
    private static readonly Uri Endpoint = new("https://api.radioreference.com/soap2/index.php?v=latest&s=rpc");
    private static readonly XNamespace SoapEnvelope = "http://schemas.xmlsoap.org/soap/envelope/";
    private static readonly XNamespace SoapEncoding = "http://schemas.xmlsoap.org/soap/encoding/";
    private static readonly XNamespace Xsi = "http://www.w3.org/2001/XMLSchema-instance";
    private static readonly XNamespace Xsd = "http://www.w3.org/2001/XMLSchema";
    private static readonly XNamespace Rr = Namespace;
    private readonly HttpClient _httpClient = new();
    private IReadOnlySet<string>? _apco25ExclusiveVoiceKeys;

    public async Task<string> GetUserDataAsync(RadioReferenceAuth auth)
    {
        var document = await CallAsync("getUserData", [], auth);
        var userNode = Descendants(document, "return").FirstOrDefault();
        return ElementValue(userNode, "username")
            ?? ElementValue(userNode, "userName")
            ?? ElementValue(userNode, "name")
            ?? string.Empty;
    }

    public async Task<IReadOnlyList<TrunkedSystem>> GetTrsBySysIdAsync(string sysId, RadioReferenceAuth auth)
    {
        var document = await CallAsync("getTrsBySysid", [SoapValue("sysid", sysId)], auth);
        return await FilterApco25ExclusiveSystemsAsync(ParseTrunkedSystems(document), auth);
    }

    public async Task<IReadOnlyList<TrunkedSystem>> FindTrsByStateCountyAsync(string stateQuery, string countyQuery, RadioReferenceAuth auth)
    {
        var state = await FindStateAsync(stateQuery, auth);

        if (string.IsNullOrWhiteSpace(countyQuery))
        {
            return await GetTrsByStateAsync(state.Id, auth);
        }

        var counties = await GetCountiesForStateAsync(state.Id, auth);
        var matchedCounties = counties
            .Where(c => c.Name.Contains(countyQuery, StringComparison.OrdinalIgnoreCase)
                || c.Header.Contains(countyQuery, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (matchedCounties.Count == 0)
        {
            throw new InvalidOperationException($"No counties matched '{countyQuery}' in {state.Name}.");
        }

        var systems = new List<TrunkedSystem>();
        foreach (var county in matchedCounties)
        {
            systems.AddRange(await GetTrsByCountyAsync(county.Id, auth));
        }

        return systems
            .GroupBy(s => s.Sid)
            .Select(g => g.First())
            .OrderBy(s => s.Name)
            .ToList();
    }

    public async Task<IReadOnlyList<RadioReferenceState>> GetUsStatesAsync(RadioReferenceAuth auth)
    {
        var countryList = await CallAsync("getCountryList", [], auth);
        var unitedStates = Descendants(countryList, "item", "Country")
            .FirstOrDefault(x => (ElementValue(x, "countryCode") ?? string.Empty).Equals("US", StringComparison.OrdinalIgnoreCase)
                || (ElementValue(x, "countryName") ?? string.Empty).Equals("United States", StringComparison.OrdinalIgnoreCase));

        if (unitedStates is null)
        {
            throw new InvalidOperationException("RadioReference did not return the United States country entry.");
        }

        var countryInfo = await CallAsync("getCountryInfo", [SoapValue("coid", ElementInt(unitedStates, "coid"))], auth);
        return Descendants(countryInfo, "item", "State")
            .Where(x => ElementInt(x, "stid") > 0)
            .Select(x => new RadioReferenceState
            {
                Id = ElementInt(x, "stid"),
                Name = ElementValue(x, "stateName") ?? string.Empty,
                Code = ElementValue(x, "stateCode") ?? string.Empty
            })
            .OrderBy(x => x.Name)
            .ToList();
    }

    public async Task<IReadOnlyList<RadioReferenceCounty>> GetCountiesForStateAsync(int stateId, RadioReferenceAuth auth)
    {
        var stateDocument = await CallAsync("getStateInfo", [SoapValue("stid", stateId)], auth);
        return ParseCounties(stateDocument);
    }

    public async Task<IReadOnlyList<TrunkedSystem>> GetTrsByStateAsync(int stateId, RadioReferenceAuth auth)
    {
        var stateDocument = await CallAsync("getStateInfo", [SoapValue("stid", stateId)], auth);
        return await FilterApco25ExclusiveSystemsAsync(ParseTrunkedSystems(stateDocument), auth);
    }

    public async Task<IReadOnlyList<TrunkedSystem>> GetTrsByCountyAsync(int countyId, RadioReferenceAuth auth)
    {
        var countyDocument = await CallAsync("getCountyInfo", [SoapValue("ctid", countyId)], auth);
        return await FilterApco25ExclusiveSystemsAsync(ParseTrunkedSystems(countyDocument), auth);
    }

    public async Task<TrunkedSystemDetails?> GetTrsDetailsAsync(int sid, RadioReferenceAuth auth)
    {
        var document = await CallAsync("getTrsDetails", [SoapValue("sid", sid)], auth);
        var details = Descendants(document, "return", "Trs").FirstOrDefault();
        if (details is null)
        {
            return null;
        }

        return new TrunkedSystemDetails
        {
            Name = ElementValue(details, "sName") ?? string.Empty
        };
    }

    public async Task<IReadOnlyList<Talkgroup>> GetTrsTalkgroupsAsync(int sid, RadioReferenceAuth auth)
    {
        var categories = await GetTrsTalkgroupCategoriesAsync(sid, auth);
        var document = await CallAsync("getTrsTalkgroups",
        [
            SoapValue("sid", sid),
            SoapValue("tgCid", 0),
            SoapValue("tgTag", 0),
            SoapValue("tgDec", 0)
        ], auth);

        return Descendants(document, "item", "Talkgroup")
            .Where(x => ElementInt(x, "tgDec") > 0)
            .Select(x =>
            {
                var categoryId = ElementInt(x, "tgCid");
                categories.TryGetValue(categoryId, out var category);

                return new Talkgroup
                {
                    Id = ElementInt(x, "tgId"),
                    DecimalId = ElementInt(x, "tgDec"),
                    Alpha = ElementValue(x, "tgAlpha") ?? string.Empty,
                    Description = ElementValue(x, "tgDescr") ?? string.Empty,
                    Mode = ElementValue(x, "tgMode") ?? string.Empty,
                    EncryptionCode = ElementInt(x, "enc"),
                    CategoryId = categoryId,
                    CategoryName = category.Name,
                    CategorySort = category.Sort,
                    Sort = ElementInt(x, "tgSort"),
                    TagSummary = ReadTags(x)
                };
            })
            .ToList();
    }

    public async Task<IReadOnlyList<TrunkedSite>> GetTrsSitesAsync(int sid, RadioReferenceAuth auth)
    {
        var document = await CallAsync("getTrsSites", [SoapValue("sid", sid)], auth);
        return Descendants(document, "item", "TrsSite")
            .Where(x => ElementInt(x, "siteId") > 0)
            .Select(x => new TrunkedSite
            {
                SiteId = ElementInt(x, "siteId"),
                SiteNumber = ElementValue(x, "siteNumber") ?? string.Empty,
                Description = ElementValue(x, "siteDescr") ?? string.Empty,
                Location = ElementValue(x, "siteLocation") ?? string.Empty,
                Latitude = ElementDecimal(x, "lat"),
                Longitude = ElementDecimal(x, "lon"),
                Range = ElementDecimal(x, "range"),
                Frequencies = Descendants(x, "item", "TrsSiteFreq")
                    .Where(f => ElementDecimal(f, "freq") > 0)
                    .Select(f => new TrunkedSiteFrequency
                    {
                        Lcn = ElementInt(f, "lcn"),
                        Frequency = ElementDecimal(f, "freq") ?? 0,
                        Use = ElementValue(f, "use") ?? string.Empty
                    })
                    .OrderBy(f => f.Frequency)
                    .ToList()
            })
            .OrderBy(x => x.SiteNumber)
            .ThenBy(x => x.Description)
            .ToList();
    }

    private async Task<Dictionary<int, (string Name, int Sort)>> GetTrsTalkgroupCategoriesAsync(int sid, RadioReferenceAuth auth)
    {
        var document = await CallAsync("getTrsTalkgroupCats", [SoapValue("sid", sid)], auth);
        return Descendants(document, "item", "TalkgroupCat")
            .Where(x => ElementInt(x, "tgCid") > 0)
            .GroupBy(x => ElementInt(x, "tgCid"))
            .ToDictionary(
                x => x.Key,
                x =>
                {
                    var node = x.First();
                    return (ElementValue(node, "tgCname") ?? string.Empty, ElementInt(node, "tgSort"));
                });
    }

    private async Task<RadioReferenceState> FindStateAsync(string stateQuery, RadioReferenceAuth auth)
    {
        if (string.IsNullOrWhiteSpace(stateQuery))
        {
            throw new InvalidOperationException("Enter a state code or state name.");
        }

        var states = await GetUsStatesAsync(auth);
        var query = stateQuery.Trim();
        var state = states.FirstOrDefault(s => s.Code.Equals(query, StringComparison.OrdinalIgnoreCase))
            ?? states.FirstOrDefault(s => s.Name.Equals(query, StringComparison.OrdinalIgnoreCase))
            ?? states.FirstOrDefault(s => s.Name.Contains(query, StringComparison.OrdinalIgnoreCase));

        return state ?? throw new InvalidOperationException($"No state matched '{stateQuery}'.");
    }

    private static IReadOnlyList<RadioReferenceCounty> ParseCounties(XDocument document)
    {
        return Descendants(document, "item", "County")
            .Where(x => ElementInt(x, "ctid") > 0)
            .Select(x => new RadioReferenceCounty
            {
                Id = ElementInt(x, "ctid"),
                Name = ElementValue(x, "countyName") ?? string.Empty,
                Header = ElementValue(x, "countyHeader") ?? string.Empty
            })
            .OrderBy(x => x.Name)
            .ToList();
    }

    private static IReadOnlyList<TrunkedSystem> ParseTrunkedSystems(XDocument document)
    {
        return Descendants(document, "item", "TrsListDef")
            .Where(x => ElementInt(x, "sid") > 0 && !string.IsNullOrWhiteSpace(ElementValue(x, "sName")))
            .Select(x => new TrunkedSystem
            {
                Sid = ElementInt(x, "sid"),
                Name = ElementValue(x, "sName") ?? string.Empty,
                City = ElementValue(x, "sCity") ?? string.Empty,
                TypeId = ElementInt(x, "sType"),
                FlavorId = ElementInt(x, "sFlavor"),
                VoiceId = ElementInt(x, "sVoice"),
                LastUpdated = ElementDate(x, "lastUpdated")
            })
            .GroupBy(x => x.Sid)
            .Select(x => x.First())
            .OrderBy(x => x.Name)
            .ToList();
    }

    private async Task<IReadOnlyList<TrunkedSystem>> FilterApco25ExclusiveSystemsAsync(IReadOnlyList<TrunkedSystem> systems, RadioReferenceAuth auth)
    {
        var voiceKeys = await GetApco25ExclusiveVoiceKeysAsync(auth);
        return systems
            .Where(s => IsApco25ExclusiveSystem(s, voiceKeys))
            .ToList();
    }

    private async Task<IReadOnlySet<string>> GetApco25ExclusiveVoiceKeysAsync(RadioReferenceAuth auth)
    {
        if (_apco25ExclusiveVoiceKeys is not null)
        {
            return _apco25ExclusiveVoiceKeys;
        }

        try
        {
            var voiceDocument = await CallAsync("getTrsVoice", [SoapValue("id", 0)], auth);
            _apco25ExclusiveVoiceKeys = Descendants(voiceDocument, "item", "trsVoiceDef")
                .Where(x => IsApco25ExclusiveDescription(ElementValue(x, "sVoiceDescr")))
                .Select(x => VoiceKey(ElementInt(x, "sType"), ElementInt(x, "sVoice")))
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
        }
        catch
        {
            _apco25ExclusiveVoiceKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        return _apco25ExclusiveVoiceKeys;
    }

    private static bool IsApco25ExclusiveSystem(TrunkedSystem system, IReadOnlySet<string> voiceKeys)
    {
        return voiceKeys.Contains(VoiceKey(system.TypeId, system.VoiceId));
    }

    private static bool IsApco25ExclusiveDescription(string? value)
    {
        return !string.IsNullOrWhiteSpace(value)
            && value.Trim().Equals("APCO-25 Common Air Interface Exclusive", StringComparison.OrdinalIgnoreCase);
    }

    private static string VoiceKey(int typeId, int voiceId)
    {
        return typeId > 0 && voiceId > 0 ? $"{typeId}:{voiceId}" : string.Empty;
    }

    private async Task<XDocument> CallAsync(string operation, IReadOnlyList<XElement> parameters, RadioReferenceAuth auth)
    {
        auth.Validate();

        var envelope = new XDocument(
            new XElement(SoapEnvelope + "Envelope",
                new XAttribute(XNamespace.Xmlns + "SOAP-ENV", SoapEnvelope),
                new XAttribute(XNamespace.Xmlns + "SOAP-ENC", SoapEncoding),
                new XAttribute(XNamespace.Xmlns + "xsi", Xsi),
                new XAttribute(XNamespace.Xmlns + "xsd", Xsd),
                new XElement(SoapEnvelope + "Body",
                    new XElement(Rr + operation,
                        new XAttribute(XNamespace.Xmlns + "ns1", Rr),
                        parameters,
                        AuthElement(auth)))));

        using var request = new HttpRequestMessage(HttpMethod.Post, Endpoint);
        request.Headers.Add("SOAPAction", $"{Namespace}#{operation}");
        request.Content = new StringContent(envelope.ToString(SaveOptions.DisableFormatting), Encoding.UTF8, "text/xml");

        using var response = await _httpClient.SendAsync(request);
        var responseText = await response.Content.ReadAsStringAsync();
        var document = ParseSoapResponse(responseText);
        var fault = Descendants(document, "Fault").FirstOrDefault();
        if (fault is not null)
        {
            var faultString = ElementValue(fault, "faultstring") ?? fault.Value.Trim();
            throw new InvalidOperationException($"RadioReference SOAP fault: {faultString}");
        }

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"RadioReference returned HTTP {(int)response.StatusCode}: {response.ReasonPhrase}");
        }

        return document;
    }

    private static XElement AuthElement(RadioReferenceAuth auth)
    {
        return new XElement("authInfo",
            new XAttribute(Xsi + "type", "ns1:authInfo"),
            SoapValue("username", auth.Username),
            SoapValue("password", auth.Password),
            SoapValue("appKey", auth.AppKey),
            SoapValue("version", auth.Version),
            SoapValue("style", "rpc"));
    }

    private static XElement SoapValue(string name, int value)
    {
        return new XElement(name, new XAttribute(Xsi + "type", "xsd:int"), value.ToString(CultureInfo.InvariantCulture));
    }

    private static XElement SoapValue(string name, string value)
    {
        return new XElement(name, new XAttribute(Xsi + "type", "xsd:string"), value);
    }

    private static XDocument ParseSoapResponse(string responseText)
    {
        try
        {
            return XDocument.Parse(responseText);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("RadioReference returned a response that could not be parsed as XML.", ex);
        }
    }

    private static IEnumerable<XElement> Descendants(XContainer container, params string[] localNames)
    {
        var names = localNames.ToHashSet(StringComparer.OrdinalIgnoreCase);
        return container.Descendants().Where(x => names.Contains(x.Name.LocalName));
    }

    private static string? ElementValue(XElement? node, string localName)
    {
        return node?.Elements().FirstOrDefault(x => x.Name.LocalName.Equals(localName, StringComparison.OrdinalIgnoreCase))?.Value.Trim();
    }

    private static int ElementInt(XElement node, string localName)
    {
        return int.TryParse(ElementValue(node, localName), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
            ? value
            : 0;
    }

    private static DateTime? ElementDate(XElement node, string localName)
    {
        return DateTime.TryParse(ElementValue(node, localName), CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var value)
            ? value
            : null;
    }

    private static decimal? ElementDecimal(XElement node, string localName)
    {
        return decimal.TryParse(ElementValue(node, localName), NumberStyles.Number, CultureInfo.InvariantCulture, out var value)
            ? value
            : null;
    }

    private static string ReadTags(XElement talkgroup)
    {
        var tags = talkgroup.Elements()
            .Where(x => x.Name.LocalName.Equals("tags", StringComparison.OrdinalIgnoreCase))
            .Descendants()
            .Where(x => !x.HasElements && !string.IsNullOrWhiteSpace(x.Value))
            .Select(x => x.Value.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return string.Join(", ", tags);
    }
}
