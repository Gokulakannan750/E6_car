using E6CarSpa.Contracts;
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
        TaxLabel.Text = $"₹{r.TotalTax:N0}";
        SplitLabel.Text = $"Cash ₹{r.Cash:N0}    Card ₹{r.Card:N0}    UPI ₹{r.Upi:N0}";

        TopServicesPanel.Children.Clear();
        if (r.TopServices.Count == 0)
        {
            TopServicesPanel.Children.Add(new Label
            {
                Text = "No service sales in this range.",
                TextColor = Color.FromArgb("#888"),
                FontSize = 14
            });
        }
        else
        {
            foreach (var s in r.TopServices)
            {
                var grid = new Grid { ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto) } };
                grid.Add(new Label { Text = $"{s.Name}  ×{s.Quantity:N0}", TextColor = Color.FromArgb("#CCC"), FontSize = 14 }, 0, 0);
                grid.Add(new Label { Text = $"₹{s.Amount:N0}", TextColor = Colors.White, FontSize = 14, HorizontalOptions = LayoutOptions.End }, 1, 0);
                TopServicesPanel.Children.Add(grid);
            }
        }

        ResultPanel.IsVisible = true;
    }

    private void ShowError(string message)
    {
        ErrorLabel.Text = message;
        ErrorLabel.IsVisible = true;
    }

    private void SetBusy(bool busy)
    {
        Busy.IsRunning = busy;
        Busy.IsVisible = busy;
        LoadButton.IsEnabled = !busy;
        if (busy) ErrorLabel.IsVisible = false;
    }
}
