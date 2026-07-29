using E6CarSpa.Contracts;
using E6CarSpa.Client;
using E6CarSpa.Mobile.Services;

namespace E6CarSpa.Mobile.Pages;

public partial class ReportsPage : ContentPage
{
    public ReportsPage()
    {
        InitializeComponent();
        var today = DateTime.Today;
        FromPicker.Date = new DateTime(today.Year, today.Month, 1);
        ToPicker.Date = today;
    }

    private async void OnLoadClicked(object? sender, EventArgs e)
    {
        var from = FromPicker.Date ?? DateTime.Today;
        var to = ToPicker.Date ?? DateTime.Today;

        if (to < from)
        {
            ShowError("'To' date is before 'From' date.");
            return;
        }

        SetBusy(true);
        try
        {
            var r = await AppServices.Api.GetSalesReportAsync(from, to);
            if (r is null) return;
            Render(r);
        }
        catch (ApiException ex)
        {
            ShowError(ex.Message);
        }
        catch (Exception)
        {
            ShowError("Cannot reach the server. Check your connection and try again.");
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void Render(SalesReportDto r)
    {
        GrandLabel.Text = $"₹{r.GrandTotal:N0}";
        InvoiceCountLabel.Text = $"{r.InvoiceCount} invoice(s) • {r.From:dd MMM} – {r.To:dd MMM yyyy}";
        CollectedLabel.Text = $"₹{r.Collected:N0}";
        OutstandingLabel.Text = $"₹{r.Outstanding:N0}";
        TaxableLabel.Text = $"₹{r.TaxableValue:N0}";
        TaxLabel.Text = $"₹{r.TotalTax:N0}";
        SplitLabel.Text = $"Cash ₹{r.Cash:N0}    Card ₹{r.Card:N0}    UPI ₹{r.Upi:N0}";

        BindableLayout.SetItemsSource(DailyPanel, r.Daily);
        DailyEmptyLabel.IsVisible = r.Daily.Count == 0;

        TopServicesPanel.Children.Clear();
        if (r.TopServices.Count == 0)
        {
            TopServicesPanel.Children.Add(new Label
            {
                Text = "No service sales in this range.",
                FontSize = 14
            });
        }
        else
        {
            int rank = 0;
            foreach (var s in r.TopServices)
            {
                rank++;
                bool isTop3 = rank <= 3;

                var border = new Border
                {
                    StrokeThickness = 0,
                    StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = new CornerRadius(8) },
                    Padding = new Thickness(12, 8),
                    BackgroundColor = isTop3
                        ? Color.FromArgb("#FEF08A")   // amber highlight — matches desktop #FEF08A
                        : Colors.Transparent,
                    Margin = new Thickness(0, 2)
                };

                var grid = new Grid
                {
                    ColumnDefinitions =
                    {
                        new ColumnDefinition(new GridLength(24, GridUnitType.Absolute)),
                        new ColumnDefinition(GridLength.Star),
                        new ColumnDefinition(GridLength.Auto)
                    },
                    ColumnSpacing = 6
                };

                var rankLabel = new Label
                {
                    Text = isTop3 ? $"#{rank}" : $"{rank}.",
                    FontSize = 13,
                    FontAttributes = isTop3 ? FontAttributes.Bold : FontAttributes.None,
                    TextColor = isTop3 ? Color.FromArgb("#92400E") : Color.FromArgb("#64748B"),
                    VerticalOptions = LayoutOptions.Center
                };

                var nameLabel = new Label
                {
                    Text = $"{s.Name}  ×{s.Quantity:N0}",
                    FontSize = 14,
                    FontAttributes = isTop3 ? FontAttributes.Bold : FontAttributes.None,
                    TextColor = isTop3 ? Color.FromArgb("#0F172A") : (Color)(Application.Current!.Resources["PrimaryDark"] ?? Color.FromArgb("#1E3A5F")),
                    VerticalOptions = LayoutOptions.Center
                };

                var amtLabel = new Label
                {
                    Text = $"₹{s.Amount:N0}",
                    FontSize = 14,
                    FontAttributes = isTop3 ? FontAttributes.Bold : FontAttributes.None,
                    TextColor = isTop3 ? Color.FromArgb("#0F172A") : (Color)(Application.Current!.Resources["Primary"] ?? Color.FromArgb("#1565C0")),
                    HorizontalOptions = LayoutOptions.End,
                    VerticalOptions = LayoutOptions.Center
                };

                grid.Add(rankLabel, 0, 0);
                grid.Add(nameLabel, 1, 0);
                grid.Add(amtLabel, 2, 0);
                border.Content = grid;
                TopServicesPanel.Children.Add(border);
            }
        }

        ResultPanel.IsVisible = true;
    }

    private void ShowError(string message)
    {
        ErrorLabel.Text = message;
        ErrorLabel.IsVisible = true;
    }

    private async void OnSettingsClicked(object? sender, EventArgs e) =>
        await Shell.Current.GoToAsync("settings");

    private void SetBusy(bool busy)
    {
        Busy.IsRunning = busy;
        Busy.IsVisible = busy;
        LoadButton.IsEnabled = !busy;
        if (busy) ErrorLabel.IsVisible = false;
    }
}
