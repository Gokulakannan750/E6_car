using E6CarSpa.Client;

namespace E6CarSpa.Mobile.Services;

/// <summary>
/// Single shared API client for the whole app. Holds the JWT after login so it survives
/// navigation between tabs. A static holder keeps page construction simple (pages used by the
/// Shell need parameterless constructors). The client itself lives in E6CarSpa.Client and is
/// shared with the desktop app; the server URL comes from Settings and can be changed at
/// runtime via <see cref="IApiClient.SetBaseUrl"/> on the Settings/Login screens.
/// </summary>
public static class AppServices
{
    public static readonly IApiClient Api = new ApiClient(new HttpClient
    {
        BaseAddress = new Uri(Settings.ApiUrl),
        Timeout = TimeSpan.FromSeconds(30)
    });
}
