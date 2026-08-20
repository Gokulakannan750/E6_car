using CommunityToolkit.Mvvm.ComponentModel;
using E6CarSpa.Client;
using E6CarSpa.Contracts;

namespace E6CarSpa.Desktop.ViewModels;

/// <summary>Form data for creating or editing a showroom.</summary>
public partial class ShowroomFormViewModel(IApiClient _api) : ObservableObject
{
 [ObservableProperty] private Guid _showroomId = Guid.Empty;
 [ObservableProperty] private string _name = "";
 [ObservableProperty] private string _address = "";
 [ObservableProperty] private string _phone = "";
 [ObservableProperty] private string _contactPerson = "";
 [ObservableProperty] private string _notes = "";

 public SaveShowroomRequest ToRequest() => new(Name, Address,
 string.IsNullOrWhiteSpace(Phone) ? null : Phone,
 string.IsNullOrWhiteSpace(ContactPerson) ? null : ContactPerson,
 string.IsNullOrWhiteSpace(Notes) ? null : Notes);
}
