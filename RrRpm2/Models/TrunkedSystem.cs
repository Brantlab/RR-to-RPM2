namespace RrRpm2.Models;

public sealed class TrunkedSystem
{
    public int Sid { get; init; }
    public string Name { get; init; } = string.Empty;
    public string City { get; init; } = string.Empty;
    public int TypeId { get; init; }
    public int FlavorId { get; init; }
    public int VoiceId { get; init; }
    public DateTime? LastUpdated { get; init; }

    public string DisplayName
    {
        get
        {
            var location = string.IsNullOrWhiteSpace(City) ? string.Empty : $" - {City}";
            return $"{Sid}: {Name}{location}";
        }
    }
}
