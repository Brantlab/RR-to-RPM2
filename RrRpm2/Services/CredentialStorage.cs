using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using RrRpm2.Models;

namespace RrRpm2.Services;

public static class CredentialStorage
{
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("RR-RPM2 RadioReference credentials");
    private static readonly string CredentialPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "RR-RPM2",
        "credentials.dat");

    public static bool Exists => File.Exists(CredentialPath);

    public static void Save(SavedCredentials credentials)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(CredentialPath)!);
        var json = JsonSerializer.Serialize(credentials);
        var bytes = Encoding.UTF8.GetBytes(json);
        var protectedBytes = ProtectedData.Protect(bytes, Entropy, DataProtectionScope.CurrentUser);
        File.WriteAllBytes(CredentialPath, protectedBytes);
    }

    public static SavedCredentials? Load()
    {
        if (!File.Exists(CredentialPath))
        {
            return null;
        }

        var protectedBytes = File.ReadAllBytes(CredentialPath);
        var bytes = ProtectedData.Unprotect(protectedBytes, Entropy, DataProtectionScope.CurrentUser);
        var json = Encoding.UTF8.GetString(bytes);
        return JsonSerializer.Deserialize<SavedCredentials>(json);
    }

    public static void Delete()
    {
        if (File.Exists(CredentialPath))
        {
            File.Delete(CredentialPath);
        }
    }
}
