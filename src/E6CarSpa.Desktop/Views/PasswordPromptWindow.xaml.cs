using System.Windows;

namespace E6CarSpa.Desktop.Views;

/// <summary>Small modal that collects a new password — used by an admin resetting a staff login.</summary>
public partial class PasswordPromptWindow : Window
{
    /// <summary>The entered password; only meaningful when the dialog returned true.</summary>
    public string Password { get; private set; } = "";

    public PasswordPromptWindow(string prompt)
    {
        InitializeComponent();
        PromptText.Text = prompt;
        Loaded += (_, _) => PasswordBox.Focus();
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        if (PasswordBox.Password.Length < 8)
        {
            ErrorText.Text = "Password must be at least 8 characters.";
            ErrorText.Visibility = Visibility.Visible;
            return;
        }

        Password = PasswordBox.Password;
        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
