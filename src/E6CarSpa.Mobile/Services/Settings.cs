namespace E6CarSpa.Mobile.Services;

/// <summary>
/// Small wrapper over MAUI <see cref="Preferences"/> for values that must survive app restarts:
/// the API base URL (the VPS address) and the last username typed, for convenience.
/// </summary>
public static class Settings
{
 // No hardcoded default: prompt the user on first launch so the app doesn't silently
 // phone home to a stale LAN address.
 public static string ApiUrl
 {
 get => Preferences.Get(nameof(ApiUrl), "");
 set => Preferences.Set(nameof(ApiUrl), string.IsNullOrWhiteSpace(value) ? "" : value.Trim());
 }

 public static bool HasApiUrl => !string.IsNullOrWhiteSpace(ApiUrl);

 /// <summary>Returns a usable default API URL when none has been saved yet.
 /// On the Android emulator, 10.0.2.2 is the alias for the host machine's localhost.
 /// On a physical device or production, the user must enter their server IP manually.</summary>
 public static string GetDefaultApiUrl()
 {
 if (DeviceInfo.Platform == DevicePlatform.Android && DeviceInfo.DeviceType == DeviceType.Virtual)
 return "http://10.0.2.2:5080";
 return "";
 }

 public static string LastUsername
 {
 get => Preferences.Get(nameof(LastUsername), "");
 set => Preferences.Set(nameof(LastUsername), value ?? "");
 }
}
