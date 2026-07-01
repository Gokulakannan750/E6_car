using Microsoft.Maui.Controls;
using System.Threading.Tasks;

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

        // Start them off-screen
        LeftLogo.TranslationX = -this.Width;
        RightLogo.TranslationX = this.Width;

        // Animate them sliding into the center (0)
        await Task.WhenAll(
            LeftLogo.TranslateTo(0, 0, 800, Easing.CubicOut),
            RightLogo.TranslateTo(0, 0, 800, Easing.CubicOut)
        );

        // Pause so the user can see the logo
        await Task.Delay(1000);

        // Transition to the login screen (it swaps to AppShell itself on a successful sign-in).
        if (Application.Current?.Windows.Count > 0)
            Application.Current.Windows[0].Page = new LoginPage();
    }
}
