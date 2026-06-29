namespace E6CarSpa.Mobile.Services;

/// <summary>
/// Small wrapper over MAUI <see cref="Preferences"/> for values that must survive app restarts:
/// the API base URL (the VPS address) and the last username typed, for convenience.
/// </summary>
public static class Settings
{
    // Default points at the Android emulator's host loopback (10.0.2.2 = the dev PC's localhost).
    // In production the owner sets this to the public HTTPS VPS address on the Settings screen.
    private const string DefaultApiUrl = "http://10.0.2.2:5080";

    public static string ApiUrl
    {
        get => Preferences.Get(nameof(ApiUrl), DefaultApiUrl);
        set => Preferences.Set(nameof(ApiUrl), string.IsNullOrWhiteSpace(value) ? DefaultApiUrl : value.Trim());
    }

    public static string LastUsername
    {
        get => Preferences.Get(nameof(LastUsername), "");
        set => Preferences.Set(nameof(LastUsername), value ?? "");
    }
}
