using System.Linq;
using System.Windows;
using System.Windows.Input;

namespace E6CarSpa.Desktop.Views;

/// <summary>Small modal to add a service to the catalogue: just a name and a price.</summary>
public partial class NewServiceDialog : Window
{
    /// <summary>Trimmed service name — only valid once ShowDialog() returned true.</summary>
    public string ServiceName { get; private set; } = "";

    /// <summary>Entered price — only valid once ShowDialog() returned true.</summary>
    public decimal Price { get; private set; }

    public NewServiceDialog()
    {
        InitializeComponent();
        Loaded += (_, _) => NameBox.Focus();
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        var name = NameBox.Text?.Trim() ?? "";
        if (name.Length == 0)
        {
            ShowError("Enter a service name.");
            NameBox.Focus();
            return;
        }
        if (!decimal.TryParse(PriceBox.Text?.Trim(), out var price) || price < 0)
        {
            ShowError("Enter a valid price (numbers only).");
            PriceBox.Focus();
            return;
        }

        ServiceName = name;
        Price = price;
        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;

    private void ShowError(string message)
    {
        ErrorText.Text = message;
        ErrorText.Visibility = Visibility.Visible;
    }

    // Price: digits and a single decimal point only.
    private void Price_PreviewTextInput(object sender, TextCompositionEventArgs e) =>
        e.Handled = !e.Text.All(c => char.IsDigit(c) || c == '.');
}
