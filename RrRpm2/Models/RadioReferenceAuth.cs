namespace RrRpm2.Models;

public sealed record RadioReferenceAuth(
    string Username,
    string Password,
    string AppKey,
    string Version)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Username))
        {
            throw new InvalidOperationException("RadioReference username is required.");
        }

        if (string.IsNullOrWhiteSpace(Password))
        {
            throw new InvalidOperationException("RadioReference password is required.");
        }

        if (string.IsNullOrWhiteSpace(AppKey))
        {
            throw new InvalidOperationException("RadioReference API app key is required.");
        }
    }
}
