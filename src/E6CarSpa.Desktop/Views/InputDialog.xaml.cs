using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;

namespace E6CarSpa.Desktop.Views;

public partial class InputDialog : Window, INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    public string PromptText { get; }
    private string _inputText = "";

    public string InputText
    {
        get => _inputText;
        set { _inputText = value; OnPropertyChanged(); }
    }

    public InputDialog(string prompt, string title, string defaultValue)
    {
        PromptText = prompt;
        InputText = defaultValue ?? "";
        DataContext = this;
        InitializeComponent();
        Title = title;
    }

    private void OnOk(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }

    /// <summary>
    /// Show a simple modal prompt dialog. Returns the typed string, or null if the user cancelled.
    /// </summary>
    public static string? Show(string prompt, string title, string? defaultValue = "")
    {
        var owner = Application.Current.MainWindow;
        var dlg = new InputDialog(prompt, title, defaultValue ?? "");
        dlg.Owner = owner;
        return dlg.ShowDialog() == true ? dlg.InputText : null;
    }

    protected void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name ?? ""));
}
