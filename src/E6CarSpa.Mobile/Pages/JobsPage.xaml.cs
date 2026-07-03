using E6CarSpa.Contracts;
using E6CarSpa.Domain.Enums;
using E6CarSpa.Client;
using E6CarSpa.Mobile.Services;

namespace E6CarSpa.Mobile.Pages;

public partial class JobsPage : ContentPage
{
    // Index in StatusPicker → status filter (null = All).
    private static readonly (string Label, InvoiceStatus? Status)[] Filters =
    {
        ("All", null),
        ("Quotation", InvoiceStatus.Quotation),
        ("Invoiced", InvoiceStatus.Invoiced),
        ("Paid", InvoiceStatus.Paid),
        ("Cancelled", InvoiceStatus.Cancelled),
    };

    public JobsPage()
    {
        InitializeComponent();
        foreach (var f in Filters) StatusPicker.Items.Add(f.Label);
        StatusPicker.SelectedIndex = 0;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        ThemeColors.ApplyTo(StatusPicker);
        // Reload every time the tab is shown so newly created/paid jobs appear.
        await LoadAsync();
    }

    private async void OnReload(object? sender, EventArgs e) => await LoadAsync();

    private async void OnRefreshing(object? sender, EventArgs e)
    {
        await LoadAsync();
        Refresh.IsRefreshing = false;
    }

    private async Task LoadAsync()
    {
        ErrorLabel.IsVisible = false;
        var status = StatusPicker.SelectedIndex >= 0 ? Filters[StatusPicker.SelectedIndex].Status : null;
        var search = string.IsNullOrWhiteSpace(Search.Text) ? null : Search.Text.Trim();
        try
        {
            var items = await AppServices.Api.ListInvoicesAsync(status, search) ?? new();
            List.ItemsSource = items;
            EmptyLabel.IsVisible = items.Count == 0;
        }
        catch (Exception ex)
        {
            ErrorLabel.Text = ex is ApiException a ? a.Message : "Cannot reach the server.";
            ErrorLabel.IsVisible = true;
        }
    }

    private async void OnSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is not InvoiceListItemDto item) return;
        List.SelectedItem = null; // clear highlight so re-tapping the same row works
        await Shell.Current.GoToAsync($"invoice?id={item.Id}");
    }
}
