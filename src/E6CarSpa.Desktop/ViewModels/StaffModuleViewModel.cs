using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using E6CarSpa.Desktop.Views;
using Microsoft.Extensions.DependencyInjection;

namespace E6CarSpa.Desktop.ViewModels;

/// <summary>Container for the Staff module with tabbed sub-pages.</summary>
public partial class StaffModuleViewModel : ObservableObject, IAsyncInitialize
{
    [ObservableProperty] private int _selectedTab = 0;
    [ObservableProperty] private object? _currentView;

    public ObservableCollection<string> Tabs { get; } = new() { "Advances", "Staff Details" };

    private StaffAdvancesViewModel _advancesVm = default!;
    private StaffMasterViewModel _staffVm = default!;
    private bool _subVmsLoaded;

    // Cache view instances to preserve UI state across tab switches
    private StaffAdvancesView? _advancesView;
    private StaffMasterView? _staffView;

    private static IServiceProvider Services => App.Services;

    partial void OnSelectedTabChanged(int value)
    {
        LoadSubVms();

        _ = value switch
        {
            0 => _advancesVm.InitializeAsync(),
            1 => _staffVm.InitializeAsync(),
            _ => Task.CompletedTask
        };

        CurrentView = value switch
        {
            0 => GetOrCreateAdvancesView(),
            1 => GetOrCreateStaffView(),
            _ => GetOrCreateAdvancesView()
        };
    }

    public async Task InitializeAsync()
    {
        LoadSubVms();
        await InitializeSubVmAsync(_advancesVm, "advances");
        await InitializeSubVmAsync(_staffVm, "staff details");

        CurrentView = GetOrCreateAdvancesView();
    }

    private StaffAdvancesView GetOrCreateAdvancesView()
    {
        return _advancesView ??= new StaffAdvancesView { DataContext = _advancesVm };
    }

    private StaffMasterView GetOrCreateStaffView()
    {
        return _staffView ??= new StaffMasterView { DataContext = _staffVm };
    }

    private static async Task InitializeSubVmAsync(object vm, string name)
    {
        if (vm is not IAsyncInitialize init) return;
        try
        {
            await init.InitializeAsync();
        }
        catch (Exception ex)
        {
            if (vm is ObservableObject oo)
            {
                var errorProp = oo.GetType().GetProperty("Error");
                if (errorProp is not null && errorProp.PropertyType == typeof(string))
                    errorProp.SetValue(oo, $"Failed to load {name}: {ex.Message}");
            }
        }
    }

    private void LoadSubVms()
    {
        if (_subVmsLoaded) return;
        _advancesVm = Services.GetRequiredService<StaffAdvancesViewModel>();
        _staffVm = Services.GetRequiredService<StaffMasterViewModel>();
        _subVmsLoaded = true;
    }
}
