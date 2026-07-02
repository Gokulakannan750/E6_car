namespace E6CarSpa.Mobile.Pages;

public partial class SplashPage : ContentPage
{
    public SplashPage()
    {
        InitializeComponent();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        try
        {
            // Start them off-screen
            LeftLogo.TranslationX = -this.Width;
            RightLogo.TranslationX = this.Width;

            // Animate them sliding into the center (0)
            await Task.WhenAll(
                LeftLogo.TranslateToAsync(0, 0, 800, Easing.CubicOut),
                RightLogo.TranslateToAsync(0, 0, 800, Easing.CubicOut)
            );

            // Pause so the user can see the logo
            await Task.Delay(1000);

            // Transition to the login screen
            if (Application.Current?.Windows.Count > 0)
                Application.Current.Windows[0].Page = new LoginPage();
        }
        catch (Exception)
        {
            // If animation fails, still navigate to login.
            if (Application.Current?.Windows.Count > 0)
                Application.Current.Windows[0].Page = new LoginPage();
        }
    }
}
