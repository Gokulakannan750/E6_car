using E6CarSpa.Mobile.Services;

namespace E6CarSpa.Mobile.Pages;

public partial class DashboardPage : ContentPage
{
    private bool _loadedOnce;

    public DashboardPage()
    {
        InitializeComponent();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (!_loadedOnce)
        {
            _loadedOnce = true;
            await LoadAsync();
        }
    }

    private async void OnRefreshing(object? sender, EventArgs e)
    {
        await LoadAsync();
        Refresh.IsRefreshing = false;
    }

    private async void OnLowStockTapped(object? sender, TappedEventArgs e) =>
        await Shell.Current.GoToAsync("lowstock");

    private async Task LoadAsync()
    {
        ErrorLabel.IsVisible = false;
        try
        {
            var d = await AppServices.Api.GetDashboardAsync();
            if (d is null) return;

            DateLabel.Text = d.Date.ToString("dddd, dd MMM yyyy");
            CollectedLabel.Text = $"₹{d.CollectedToday:N0}";
            SplitLabel.Text = $"Cash ₹{d.CashToday:N0}   •   Card ₹{d.CardToday:N0}   •   UPI ₹{d.UpiToday:N0}";

            JobsLabel.Text = d.JobsToday.ToString();
            QuotationsLabel.Text = d.QuotationsPending.ToString();
            UnpaidLabel.Text = d.InvoicesUnpaid.ToString();
            LowStockLabel.Text = d.LowStockCount.ToString();

            JobsList.ItemsSource = d.ActiveJobs;
            NoJobsLabel.IsVisible = d.ActiveJobs.Count == 0;
        }
        catch (ApiException ex)
        {
            ShowError(ex.Message);
        }
        catch (Exception)
        {
            ShowError("Cannot reach the server. Pull down to retry.");
        }
    }

    private void ShowError(string message)
    {
        ErrorLabel.Text = message;
        ErrorLabel.IsVisible = true;
    }
}
