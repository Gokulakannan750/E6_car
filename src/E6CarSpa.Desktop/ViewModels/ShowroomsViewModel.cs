using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using E6CarSpa.Client;
using E6CarSpa.Contracts;
using E6CarSpa.Desktop.Views;

namespace E6CarSpa.Desktop.ViewModels;

/// <summary>
/// Manage showroom master records.
/// </summary>
public partial class ShowroomsViewModel(IApiClient api) : ObservableObject, IAsyncInitialize
{
 public ObservableCollection<ShowroomDto> Showrooms { get; } = new();

 [ObservableProperty] private string _search = "";
 [ObservableProperty] private bool _showInactive;
 [ObservableProperty] private bool _isBusy;
 [ObservableProperty] private string _error = "";
 [ObservableProperty] private string _info = "";

 partial void OnShowInactiveChanged(bool value) => _ = LoadAsync();
 partial void OnSearchChanged(string value) => _ = DebouncedSearchAsync();

 private int _searchGen;

 private async Task DebouncedSearchAsync()
 {
 var gen = ++_searchGen;
 await Task.Delay(300);
 if (gen != _searchGen) return;
 await LoadAsync();
 }

 public Task InitializeAsync() => LoadAsync();

 [RelayCommand]
 private async Task LoadAsync()
 {
 try
 {
 IsBusy = true; Error = "";

 var list = await api.GetShowroomsAsync(ShowInactive)
 ?? new();
 Showrooms.Clear();
 foreach (var s in list) Showrooms.Add(s);
 }
 catch (Exception ex) { Error = ex.Message; }
 finally { IsBusy = false; }
 }

 [RelayCommand]
 private async Task AddShowroomAsync()
 {
 var vm = new ShowroomFormViewModel(api);
 var dlg = new ShowroomFormView { DataContext = vm, Owner = Application.Current.MainWindow };
 if (dlg.ShowDialog() != true) return;

 try
 {
 IsBusy = true; Info = "";
 await api.CreateShowroomAsync(vm.ToRequest());
 Info = $"Showroom \"{vm.Name}\" created.";
 await LoadAsync();
 }
 catch (Exception ex) { Error = ex.Message; }
 finally { IsBusy = false; }
 }

 [RelayCommand]
 private async Task EditShowroomAsync(ShowroomDto? showroom)
 {
 if (showroom is null) return;

 var vm = new ShowroomFormViewModel(api)
 {
 ShowroomId = showroom.Id,
 Name = showroom.Name,
 Address = showroom.Address,
 Phone = showroom.Phone,
 ContactPerson = showroom.ContactPerson,
 Notes = showroom.Notes
 };

 var dlg = new ShowroomFormView { DataContext = vm, Owner = Application.Current.MainWindow };
 if (dlg.ShowDialog() != true) return;

 try
 {
 IsBusy = true; Info = "";
 await api.UpdateShowroomAsync(showroom.Id, vm.ToRequest());
 Info = $"Showroom \"{vm.Name}\" updated.";
 await LoadAsync();
 }
 catch (Exception ex) { Error = ex.Message; }
 finally { IsBusy = false; }
 }

 [RelayCommand]
 private async Task ToggleActiveAsync(ShowroomDto? showroom)
 {
 if (showroom is null) return;

 try
 {
 IsBusy = true; Info = "";
 if (showroom.IsActive)
 {
 await api.DeactivateShowroomAsync(showroom.Id);
 Info = $"\"{showroom.Name}\" deactivated.";
 }
 else
 {
 await api.RestoreShowroomAsync(showroom.Id);
 Info = $"\"{showroom.Name}\" reactivated.";
 }
 await LoadAsync();
 }
 catch (Exception ex) { Error = ex.Message; }
 finally { IsBusy = false; }
 }
}
