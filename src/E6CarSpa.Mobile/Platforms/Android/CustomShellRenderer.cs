#if ANDROID
using Microsoft.Maui.Controls.Handlers.Compatibility;
using Microsoft.Maui.Controls.Platform.Compatibility;
using Google.Android.Material.BottomNavigation;
using Android.Views;

namespace E6CarSpa.Mobile.Platforms.Android;

public class CustomShellRenderer : ShellRenderer
{
    protected override IShellBottomNavViewAppearanceTracker CreateBottomNavViewAppearanceTracker(ShellItem shellItem)
    {
        return new CustomBottomNavViewAppearanceTracker(this, shellItem);
    }
}

public class CustomBottomNavViewAppearanceTracker : ShellBottomNavViewAppearanceTracker
{
    public CustomBottomNavViewAppearanceTracker(IShellContext shellContext, ShellItem shellItem) : base(shellContext, shellItem)
    {
    }

    public override void SetAppearance(BottomNavigationView bottomView, IShellAppearanceElement appearance)
    {
        base.SetAppearance(bottomView, appearance);
        // Remove the default tinting so the multi-colored SVG icons display in their original colors
        bottomView.ItemIconTintList = null;
    }
}
#endif
