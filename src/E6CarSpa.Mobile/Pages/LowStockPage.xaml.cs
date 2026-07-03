using E6CarSpa.Client;
using E6CarSpa.Mobile.Services;

namespace E6CarSpa.Mobile.Pages;

public partial class LowStockPage : ContentPage
{
    private readonly ThemeRowRefresher _themeRows = new();
    private bool _loadedOnce;

    public LowStockPage()
    {
        InitializeComponent();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (_themeRows.RowsAreStale() && List.ItemsSource is { } rows)
        {
            List.ItemsSource = null;
            List.ItemsSource = rows;
        }
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

    private async Task LoadAsync()
    {
        ErrorLabel.IsVisible = false;
        try
        {
            var products = await AppServices.Api.GetProductsAsync(lowStockOnly: true) ?? new();
            List.ItemsSource = products;
            EmptyLabel.IsVisible = products.Count == 0;
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
