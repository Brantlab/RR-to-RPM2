namespace RrRpm2.Models;

public sealed class TrunkedSite
{
    public int SiteId { get; init; }
    public string SiteNumber { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string Location { get; init; } = string.Empty;
    public decimal? Latitude { get; init; }
    public decimal? Longitude { get; init; }
    public decimal? Range { get; init; }
    public IReadOnlyList<TrunkedSiteFrequency> Frequencies { get; init; } = [];

    public IReadOnlyList<TrunkedSiteFrequency> ControlFrequencies =>
        Frequencies.Where(f => f.IsControlChannel).ToList();

    public string DisplayName
    {
        get
        {
            var number = string.IsNullOrWhiteSpace(SiteNumber) ? SiteId.ToString() : SiteNumber;
            var location = string.IsNullOrWhiteSpace(Location) ? string.Empty : $" - {Location}";
            return $"{number}: {Description}{location} ({ControlFrequencies.Count} CC)";
        }
    }
}
