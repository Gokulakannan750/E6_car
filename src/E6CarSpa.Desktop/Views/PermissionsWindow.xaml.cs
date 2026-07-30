using System.Windows;
using E6CarSpa.Client;
using E6CarSpa.Domain.Enums;

namespace E6CarSpa.Desktop.Views;

/// <summary>Tick-list editor for one user's permissions.</summary>
public partial class PermissionsWindow : Window
{
    private readonly List<PermissionOption> _options;

    /// <summary>The chosen permissions; only meaningful when the dialog returned true.</summary>
    public Permission Result { get; private set; }

    public PermissionsWindow(string username, Permission current)
    {
        InitializeComponent();
        TitleText.Text = $"Permissions for '{username}'";
        _options = PermissionOption.BuildList(current);
        PermissionList.ItemsSource = _options;
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        Result = PermissionOption.Combine(_options);
        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
