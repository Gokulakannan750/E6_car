using System.Net.Http;
using System.Windows;
using E6CarSpa.Client;
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

        InstallCrashHandlers();
        AppLog.Prune();
        AppLog.Info("Application starting.");

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
        services.AddTransient<StaffAdvancesViewModel>();

        // Windows
        services.AddTransient<LoginWindow>();
        services.AddSingleton<ShellWindow>();

        Services = services.BuildServiceProvider();

        var api = Services.GetRequiredService<IApiClient>();
        // The token expired or was revoked server-side (deactivated user / rotated security
        // stamp). Re-authenticate in place; if the user declines, close the app rather than
        // leaving a signed-out shell on screen.
        // Second line of defence behind ApiClient's once-per-session guard: a request already in
        // flight when the session was replaced can still come back 401 afterwards, and must not
        // throw a second login dialog over the first. Only ever touched on the UI thread, so a
        // plain flag is enough.
        var reauthenticating = false;
        api.OnUnauthorized += () =>
        {
            Dispatcher.Invoke(() =>
            {
                if (reauthenticating) return;

                var shell = Services.GetService<ShellWindow>();
                if (shell is null || !shell.IsVisible) return;   // startup gate handles its own login

                reauthenticating = true;
                try
                {
                    AppLog.Info("Session rejected by the server; asking the user to sign in again.");
                    var relogin = Services.GetRequiredService<LoginWindow>();
                    relogin.Owner = shell;
                    if (relogin.ShowDialog() != true)
                        Shutdown();
                }
                finally { reauthenticating = false; }
            });
        };

        CleanupOldPdfs();

        // Login-first: the app is not usable until a real user authenticates. (Previously the
        // shell opened straight into an anonymous counter session and only prompted on 401.)
        // Closing the login window without signing in exits the app rather than revealing the shell.
        var login = Services.GetRequiredService<LoginWindow>();
        if (login.ShowDialog() != true)
        {
            Shutdown();
            return;
        }

        var shellWindow = Services.GetRequiredService<ShellWindow>();
        shellWindow.Show();
    }

    /// <summary>
    /// Catch what would otherwise be a silent death.
    ///
    /// Previously an unhandled exception closed the app with the default Windows dialog, mid-job,
    /// leaving no trace to diagnose. A billing terminal losing what the counter was typing is a
    /// real cost, so UI-thread failures are now logged, explained, and survived where possible.
    /// </summary>
    private void InstallCrashHandlers()
    {
        // UI thread. Marking it handled keeps the app alive — for a counter terminal, a usable
        // app with one failed action beats losing the half-entered invoice behind it.
        DispatcherUnhandledException += (_, args) =>
        {
            AppLog.Error("Unhandled exception on the UI thread.", args.Exception);
            args.Handled = true;
            MessageBox.Show(
                "Something went wrong with that action, and it has been recorded in the log.\n\n" +
                "You can carry on working. If it keeps happening, send the log folder to support:\n" +
                AppLog.Folder,
                "E6 Car Spa", MessageBoxButton.OK, MessageBoxImage.Warning);
        };

        // Background threads. The runtime tears the process down regardless — all we can do is
        // leave a record behind, which is exactly what was missing before.
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            AppLog.Error("Unhandled exception on a background thread (process is terminating).",
                args.ExceptionObject as Exception);

        // A faulted Task nobody awaited — how a failing AsyncRelayCommand disappears silently.
        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            AppLog.Error("Unobserved task exception.", args.Exception);
            args.SetObserved();
        };
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
