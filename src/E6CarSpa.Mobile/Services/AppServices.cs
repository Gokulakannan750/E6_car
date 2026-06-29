namespace E6CarSpa.Mobile.Services;

/// <summary>
/// Single shared API client for the whole app. Holds the JWT after login so it survives
/// navigation between tabs. A static holder keeps page construction simple (pages used by the
/// Shell need parameterless constructors), which is plenty for a small read-only monitor app.
/// </summary>
public static class AppServices
{
    public static readonly IApiClient Api = new ApiClient();
}
