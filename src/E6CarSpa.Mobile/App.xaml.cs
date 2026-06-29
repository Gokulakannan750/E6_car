using E6CarSpa.Mobile.Pages;

namespace E6CarSpa.Mobile;

public partial class App : Application
{
	public App()
	{
		InitializeComponent();
	}

	protected override Window CreateWindow(IActivationState? activationState)
	{
		// Always start at the login screen; on success it swaps the window to the AppShell tabs.
		return new Window(new LoginPage());
	}
}