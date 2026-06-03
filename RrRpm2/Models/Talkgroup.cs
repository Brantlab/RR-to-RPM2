namespace RrRpm2.Models;

public sealed class Talkgroup
{
    public int Id { get; init; }
    public int DecimalId { get; init; }
    public string Alpha { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string Mode { get; init; } = string.Empty;
    public int EncryptionCode { get; init; }
    public int CategoryId { get; init; }
    public string CategoryName { get; init; } = string.Empty;
    public int CategorySort { get; init; }
    public int Sort { get; init; }
    public string TagSummary { get; init; } = string.Empty;

    public bool IsEncrypted =>
        EncryptionCode != 0 ||
        Mode.Equals("E", StringComparison.OrdinalIgnoreCase) ||
        Mode.Contains("enc", StringComparison.OrdinalIgnoreCase);
}
