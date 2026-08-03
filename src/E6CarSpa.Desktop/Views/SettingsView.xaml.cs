using System.Windows;
using System.Windows.Controls;
using E6CarSpa.Contracts;
using E6CarSpa.Desktop.Services;
using E6CarSpa.Desktop.ViewModels;

namespace E6CarSpa.Desktop.Views;

public partial class SettingsView : UserControl
{
    private SettingsViewModel? _subscribed;

    public SettingsView()
    {
        InitializeComponent();

        // The view model arrives after construction, so hook its events when it does.
        DataContextChanged += (_, _) =>
        {
            if (_subscribed is not null) _subscribed.OwnPasswordChanged -= OnOwnPasswordChanged;
            _subscribed = Vm;
            if (_subscribed is not null) _subscribed.OwnPasswordChanged += OnOwnPasswordChanged;
        };
    }

    private SettingsViewModel? Vm => DataContext as SettingsViewModel;

    /// <summary>
    /// Changing your own password revokes this session's token server-side, so go back to the login
    /// window rather than leaving a shell on screen whose every request would now fail.
    /// </summary>
    private void OnOwnPasswordChanged()
    {
        if (Window.GetWindow(this) is ShellWindow shell)
            shell.SignOutAndReauthenticate("password changed — please sign in again");
    }

    private void NewUserPassword_Changed(object sender, RoutedEventArgs e)
    {
        if (Vm is not null) Vm.NewUserPassword = NewUserPasswordBox.Password;
    }

    private async void EditPermissions_Click(object sender, RoutedEventArgs e)
    {
        if (Vm is null || (sender as FrameworkElement)?.DataContext is not UserDto user) return;

        var dialog = new PermissionsWindow(user.Username, user.Permissions)
        {
            Owner = Window.GetWindow(this)
        };
        if (dialog.ShowDialog() != true) return;

        await Vm.SaveUserPermissionsAsync(user, dialog.Result);
    }

    private async void ResetUserPassword_Click(object sender, RoutedEventArgs e)
    {
        if (Vm is null || (sender as FrameworkElement)?.DataContext is not UserDto user) return;

        var dialog = new PasswordPromptWindow($"Set a new password for '{user.Username}'")
        {
            Owner = Window.GetWindow(this)
        };
        if (dialog.ShowDialog() != true) return;

        await Vm.ResetUserPasswordAsync(user, dialog.Password);
    }

    private void MyOldPassword_Changed(object sender, RoutedEventArgs e)
    {
        if (Vm is not null) Vm.MyOldPassword = MyOldPasswordBox.Password;
    }

    private void MyNewPassword_Changed(object sender, RoutedEventArgs e)
    {
        if (Vm is not null) Vm.MyNewPassword = MyNewPasswordBox.Password;
    }



    private async void UploadLogo_Click(object sender, RoutedEventArgs e)
    {
        if (Vm is null) return;
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Choose a logo image",
            Filter = "Images (*.png;*.jpg;*.jpeg)|*.png;*.jpg;*.jpeg"
        };
        if (dlg.ShowDialog() != true) return;

        // This runs in an `async void` handler, so an exception here cannot be caught by a
        // caller — it would reach the crash handler and interrupt whatever the counter was
        // doing. A file that is locked, deleted since the dialog opened, or unreadable is an
        // ordinary mistake, not a crash.
        try
        {
            // The API rejects anything over 2 MB. Check first rather than reading a huge file
            // into memory only to have the upload refused.
            const long maxBytes = 2 * 1024 * 1024;
            var info = new System.IO.FileInfo(dlg.FileName);
            if (info.Length > maxBytes)
            {
                Vm.Error = $"That image is {info.Length / 1024d / 1024d:0.#} MB. Please choose one under 2 MB.";
                return;
            }

            var bytes = await System.IO.File.ReadAllBytesAsync(dlg.FileName);
            await Vm.SetLogoAsync(bytes);
        }
        catch (Exception ex)
        {
            AppLog.Error($"Could not read the chosen logo file.", ex);
            Vm.Error = "Could not read that file. It may be open in another program, or you may not have permission.";
        }
    }
}
