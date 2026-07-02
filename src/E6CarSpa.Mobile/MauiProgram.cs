using Microsoft.Extensions.Logging;

namespace E6CarSpa.Mobile;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
		var builder = MauiApp.CreateBuilder();
		builder
			.UseMauiApp<App>()
			.ConfigureFonts(fonts =>
			{
				fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
				fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
			})
			.ConfigureMauiHandlers(handlers =>
			{
#if ANDROID
				handlers.AddHandler(typeof(Shell), typeof(E6CarSpa.Mobile.Platforms.Android.CustomShellRenderer));
#endif
			});

#if DEBUG
		builder.Logging.AddDebug();
#endif

		// The API client is a single shared instance held in AppServices (see Services/AppServices.cs).
		// The server URL lives in Settings (Preferences) and is editable on the Settings screen,
		// so no HttpClient is registered in DI here.

		return builder.Build();
	}
}
