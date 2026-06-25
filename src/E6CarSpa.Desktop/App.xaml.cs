using System.Net.Http;
using System.Windows;
using E6CarSpa.Desktop.Services;
using E6CarSpa.Desktop.ViewModels;
using E6CarSpa.Desktop.Views;
using Microsoft.Extensions.DependencyInjection;

namespace E6CarSpa.Desktop;

public partial class App : Application
{
    public static IServiceProvider Services { get; private set; } = default!;

    /// <summary>API base URL. Override with the E6_API_URL environment variable on the shop PC.</summary>
    private static string ApiBaseUrl =>
        Environment.GetEnvironmentVariable("E6_API_URL") ?? "http://localhost:5080/";

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var services = new ServiceCollection();

        services.AddSingleton(sp => new ApiClient(new HttpClient
        {
            BaseAddress = new Uri(ApiBaseUrl),
            Timeout = TimeSpan.FromSeconds(30)
        }));

        // ViewModels
        services.AddSingleton<ShellViewModel>();
        services.AddTransient<LoginViewModel>();
        services.AddTransient<DashboardViewModel>();
        services.AddTransient<NewJobViewModel>();
        services.AddTransient<JobsViewModel>();
        services.AddTransient<InvoiceDetailViewModel>();
        services.AddTransient<InventoryViewModel>();
        services.AddTransient<ReportsViewModel>();
        services.AddTransient<SettingsViewModel>();
        services.AddTransient<CatalogueViewModel>();

        // Windows
        services.AddTransient<LoginWindow>();
        services.AddSingleton<ShellWindow>();

        Services = services.BuildServiceProvider();

        var login = Services.GetRequiredService<LoginWindow>();
        login.Show();
    }
}
