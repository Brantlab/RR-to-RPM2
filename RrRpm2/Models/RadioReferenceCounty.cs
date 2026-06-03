namespace RrRpm2.Models;

public sealed class RadioReferenceCounty
{
    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Header { get; init; } = string.Empty;

    public string DisplayName =>
        string.IsNullOrWhiteSpace(Header) ? Name : $"{Name} - {Header}";
}
