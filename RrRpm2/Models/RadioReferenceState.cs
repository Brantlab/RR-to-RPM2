namespace RrRpm2.Models;

public sealed class RadioReferenceState
{
    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Code { get; init; } = string.Empty;

    public string DisplayName =>
        string.IsNullOrWhiteSpace(Code) ? Name : $"{Code} - {Name}";
}
