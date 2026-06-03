namespace RrRpm2.Models;

public sealed class TrunkedSiteFrequency
{
    public int Lcn { get; init; }
    public decimal Frequency { get; init; }
    public string Use { get; init; } = string.Empty;

    public bool IsControlChannel =>
        Use.Contains('c', StringComparison.OrdinalIgnoreCase) ||
        Use.Contains('a', StringComparison.OrdinalIgnoreCase);
}
