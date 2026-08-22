using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using E6CarSpa.Client;
using E6CarSpa.Contracts;

namespace E6CarSpa.Desktop.Services;

/// <summary>
/// Handles securely saving and loading the API session (Token + UserDto) 
/// using Windows Data Protection API (DPAPI) so the user stays logged in across restarts.
/// </summary>
public class SessionManager
{
    private readonly IApiClient _api;
    private readonly string _sessionFilePath;

    // Use current user scope so only the Windows user who logged in can decrypt the file.
    private const DataProtectionScope Scope = DataProtectionScope.CurrentUser;

    public SessionManager(IApiClient api)
    {
        _api = api;
        
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var appFolder = Path.Combine(appData, "E6CarSpa");
        Directory.CreateDirectory(appFolder);
        _sessionFilePath = Path.Combine(appFolder, "session.dat");

        // When the user explicitly logs out, delete the session file
        _api.OnLogout += ClearSession;
    }

    public void SaveSession()
    {
        if (!_api.IsLoggedIn || string.IsNullOrEmpty(_api.CurrentToken))
            return;

        try
        {
            var data = new SessionData
            {
                Token = _api.CurrentToken,
                User = _api.CurrentUser!,
                MustChangePassword = _api.MustChangePassword
            };

            var json = JsonSerializer.Serialize(data);
            var plainBytes = Encoding.UTF8.GetBytes(json);
            
            // Encrypt using DPAPI
            var encryptedBytes = ProtectedData.Protect(plainBytes, null, Scope);
            
            File.WriteAllBytes(_sessionFilePath, encryptedBytes);
        }
        catch (Exception ex)
        {
            AppLog.Error("Failed to save secure session via DPAPI.", ex);
        }
    }

    public bool TryRestoreSession()
    {
        if (!File.Exists(_sessionFilePath))
            return false;

        try
        {
            var encryptedBytes = File.ReadAllBytes(_sessionFilePath);
            var plainBytes = ProtectedData.Unprotect(encryptedBytes, null, Scope);
            var json = Encoding.UTF8.GetString(plainBytes);
            
            var data = JsonSerializer.Deserialize<SessionData>(json);
            if (data?.Token != null && data.User != null)
            {
                _api.RestoreSession(data.Token, data.User, data.MustChangePassword);
                return true;
            }
        }
        catch (CryptographicException)
        {
            // Decryption failed (e.g., copied from another PC or user profile)
            AppLog.Info("Session file could not be decrypted. It will be cleared.");
            ClearSession();
        }
        catch (Exception ex)
        {
            AppLog.Error("Failed to restore secure session via DPAPI.", ex);
            ClearSession();
        }

        return false;
    }

    public void ClearSession()
    {
        try
        {
            if (File.Exists(_sessionFilePath))
                File.Delete(_sessionFilePath);
        }
        catch { /* Ignore IO errors on delete */ }
    }

    private class SessionData
    {
        public string Token { get; set; } = "";
        public UserDto User { get; set; } = default!;
        public bool MustChangePassword { get; set; }
    }
}
