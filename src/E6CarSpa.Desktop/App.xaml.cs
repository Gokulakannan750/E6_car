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

        services.AddSingleton<IApiClient>(sp => new ApiClient(new HttpClient
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
        services.AddTransient<CustomersViewModel>();

        // Windows
        services.AddTransient<LoginWindow>();
        services.AddSingleton<ShellWindow>();

        Services = services.BuildServiceProvider();

        var api = Services.GetRequiredService<IApiClient>();
        api.OnUnauthorized += () =>
        {
            Dispatcher.Invoke(() =>
            {
                var shell = Services.GetService<ShellWindow>();
                var login = Services.GetRequiredService<LoginWindow>();
                if (shell != null && shell.IsVisible)
                {
                    login.Owner = shell;
                    login.ShowDialog();
                }
                else
                {
                    login.Show();
                }
            });
        };

        CleanupOldPdfs();

        var shellWindow = Services.GetRequiredService<ShellWindow>();
        shellWindow.Show();
    }

    private void CleanupOldPdfs()
    {
        try
        {
            var temp = System.IO.Path.GetTempPath();
            var files = System.IO.Directory.GetFiles(temp, "E6_*.pdf");
            foreach (var f in files)
            {
                var fi = new System.IO.FileInfo(f);
                if (fi.CreationTime < DateTime.Now.AddDays(-1))
                    fi.Delete();
            }
        }
        catch { /* ignore cleanup errors */ }
    }
}
