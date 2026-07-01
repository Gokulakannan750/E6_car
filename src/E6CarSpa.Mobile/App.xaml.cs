using E6CarSpa.Mobile.Pages;

namespace E6CarSpa.Mobile;

public partial class App : Application
{
	public App()
	{
		InitializeComponent();

		// Restore the user's saved theme preference
		var saved = Preferences.Get("AppTheme", "Light");
		UserAppTheme = saved == "Dark" ? AppTheme.Dark : AppTheme.Light;
	}

	protected override Window CreateWindow(IActivationState? activationState)
	{
		// Start with the animated splash page; it swaps the window to LoginPage when done,
		// which in turn swaps to AppShell on a successful sign-in.
		return new Window(new SplashPage());
	}
}