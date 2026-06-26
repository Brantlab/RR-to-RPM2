namespace RrRpm2.Models;

public sealed class TrunkedSiteFrequency
{
    public int Lcn { get; init; }
    public decimal Frequency { get; init; }
    public string Use { get; init; } = string.Empty;

    public bool IsControlChannel =>
        UseCategory is TrunkedSiteFrequencyUse.DedicatedControl
            or TrunkedSiteFrequencyUse.AlternateControl
            or TrunkedSiteFrequencyUse.GenericControl;

    public TrunkedSiteFrequencyUse UseCategory
    {
        get
        {
            if (Use.Contains('d', StringComparison.OrdinalIgnoreCase))
            {
                return TrunkedSiteFrequencyUse.DedicatedControl;
            }

            if (Use.Contains('a', StringComparison.OrdinalIgnoreCase))
            {
                return TrunkedSiteFrequencyUse.AlternateControl;
            }

            if (Use.Contains('c', StringComparison.OrdinalIgnoreCase))
            {
                return TrunkedSiteFrequencyUse.GenericControl;
            }

            return TrunkedSiteFrequencyUse.Other;
        }
    }
}

public enum TrunkedSiteFrequencyUse
{
    Other,
    DedicatedControl,
    AlternateControl,
    GenericControl
}
