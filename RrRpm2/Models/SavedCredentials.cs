namespace RrRpm2.Models;

public sealed class SavedCredentials
{
    public string Username { get; init; } = string.Empty;
    public string Password { get; init; } = string.Empty;
    public string AppKey { get; init; } = string.Empty;
}
