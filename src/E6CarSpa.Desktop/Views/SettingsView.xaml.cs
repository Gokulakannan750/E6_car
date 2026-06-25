using System.Windows;
using System.Windows.Controls;
using E6CarSpa.Desktop.ViewModels;

namespace E6CarSpa.Desktop.Views;

public partial class SettingsView : UserControl
{
    public SettingsView() => InitializeComponent();

    private SettingsViewModel? Vm => DataContext as SettingsViewModel;

    // PasswordBox.Password is not bindable, so push values into the VM here.
    private void MyNewPassword_Changed(object sender, RoutedEventArgs e)
    {
        if (Vm is not null) Vm.MyNewPassword = MyNewPasswordBox.Password;
    }

    private void NewPassword_Changed(object sender, RoutedEventArgs e)
    {
        if (Vm is not null) Vm.NewPassword = NewPasswordBox.Password;
    }

    private void EditPassword_Changed(object sender, RoutedEventArgs e)
    {
        if (Vm is not null) Vm.EditNewPassword = EditPasswordBox.Password;
    }

    private async void UploadLogo_Click(object sender, RoutedEventArgs e)
    {
        if (Vm is null) return;
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Choose a logo image",
            Filter = "Images (*.png;*.jpg;*.jpeg)|*.png;*.jpg;*.jpeg"
        };
        if (dlg.ShowDialog() == true)
        {
            var bytes = await System.IO.File.ReadAllBytesAsync(dlg.FileName);
            await Vm.SetLogoAsync(bytes);
        }
    }
}
