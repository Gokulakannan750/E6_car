using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using E6CarSpa.Contracts;
using E6CarSpa.Desktop.Services;
using E6CarSpa.Domain.Enums;

namespace E6CarSpa.Desktop.ViewModels;

/// <summary>Admin screen: change own password, manage staff users, edit company / GST details.</summary>
public partial class SettingsViewModel(IApiClient api) : ObservableObject, IAsyncInitialize
{
    // ----- Company settings -----
    [ObservableProperty] private string _name = "";
    [ObservableProperty] private string _addressLine1 = "";
    [ObservableProperty] private string _addressLine2 = "";
    [ObservableProperty] private string _city = "";
    [ObservableProperty] private string _state = "";
    [ObservableProperty] private string _stateCode = "";
    [ObservableProperty] private string _pincode = "";
    [ObservableProperty] private string _phone = "";
    [ObservableProperty] private string _email = "";
    [ObservableProperty] private string _gstin = "";
    [ObservableProperty] private string _invoicePrefix = "";
    [ObservableProperty] private decimal _defaultGstRate;
    [ObservableProperty] private long _lastInvoiceSequence;

    // ----- Users -----
    public ObservableCollection<UserDto> Users { get; } = new();
    public List<UserRole> Roles { get; } = [UserRole.Admin, UserRole.Manager, UserRole.Worker];

    [ObservableProperty] private UserDto? _selectedUser;
    [ObservableProperty] private string _editFullName = "";
    [ObservableProperty] private UserRole _editRole = UserRole.Worker;
    [ObservableProperty] private bool _editActive = true;
    [ObservableProperty] private string _editNewPassword = "";

    // New user
    [ObservableProperty] private string _newFullName = "";
    [ObservableProperty] private string _newUsername = "";
    [ObservableProperty] private string _newPassword = "";
    [ObservableProperty] private UserRole _newRole = UserRole.Worker;

    // Change my password
    [ObservableProperty] private string _myNewPassword = "";

    // ----- Logo -----
    [ObservableProperty] private byte[]? _logoBytes;
    public bool HasLogo => LogoBytes is { Length: > 0 };
    partial void OnLogoBytesChanged(byte[]? value) => OnPropertyChanged(nameof(HasLogo));

    [ObservableProperty] private string _info = "";
    [ObservableProperty] private string _error = "";

    public string MyUsername => api.CurrentUser?.Username ?? "";

    public async Task InitializeAsync()
    {
        await LoadSettingsAsync();
        await LoadUsersAsync();
    }

    private async Task LoadSettingsAsync()
    {
        try
        {
            var s = await api.GetSettingsAsync();
            if (s is null) return;
            Name = s.Name; AddressLine1 = s.AddressLine1 ?? ""; AddressLine2 = s.AddressLine2 ?? "";
            City = s.City ?? ""; State = s.State ?? ""; StateCode = s.StateCode ?? ""; Pincode = s.Pincode ?? "";
            Phone = s.Phone ?? ""; Email = s.Email ?? ""; Gstin = s.Gstin ?? "";
            InvoicePrefix = s.InvoicePrefix; DefaultGstRate = s.DefaultGstRate; LastInvoiceSequence = s.LastInvoiceSequence;
            LogoBytes = await api.GetLogoAsync();
        }
        catch (Exception ex) { Error = ex.Message; }
    }

    /// <summary>Called from the view after the user picks an image file.</summary>
    public async Task SetLogoAsync(byte[] bytes)
    {
        try
        {
            Info = ""; Error = "";
            await api.UploadLogoAsync(bytes);
            LogoBytes = bytes;
            Info = "Logo saved. It will appear on new invoices/quotations.";
        }
        catch (Exception ex) { Error = ex.Message; }
    }

    [RelayCommand]
    private async Task RemoveLogoAsync()
    {
        try
        {
            Info = ""; Error = "";
            await api.DeleteLogoAsync();
            LogoBytes = null;
            Info = "Logo removed.";
        }
        catch (Exception ex) { Error = ex.Message; }
    }

    private async Task LoadUsersAsync()
    {
        try
        {
            var users = await api.GetUsersAsync() ?? new();
            Users.Clear();
            foreach (var u in users) Users.Add(u);
        }
        catch (Exception ex) { Error = ex.Message; }
    }

    partial void OnSelectedUserChanged(UserDto? value)
    {
        if (value is null) return;
        EditFullName = value.FullName;
        EditRole = value.Role;
        EditActive = value.IsActive;
        EditNewPassword = "";
    }

    [RelayCommand]
    private async Task SaveSettingsAsync()
    {
        try
        {
            Info = ""; Error = "";
            var req = new SaveCompanySettingsRequest(Name, AddressLine1, AddressLine2, City, State,
                StateCode, Pincode, Phone, Email, Gstin, InvoicePrefix, DefaultGstRate);
            await api.UpdateSettingsAsync(req);
            Info = "Company settings saved.";
        }
        catch (Exception ex) { Error = ex.Message; }
    }

    [RelayCommand]
    private async Task ChangeMyPasswordAsync()
    {
        if (api.CurrentUser is null) return;
        if (string.IsNullOrWhiteSpace(MyNewPassword) || MyNewPassword.Length < 5)
        { Error = "New password must be at least 5 characters."; return; }
        try
        {
            Info = ""; Error = "";
            var u = api.CurrentUser;
            await api.UpdateUserAsync(u.Id, new UpdateUserRequest(u.FullName, u.Role, true, MyNewPassword));
            MyNewPassword = "";
            Info = "Your password has been changed.";
        }
        catch (Exception ex) { Error = ex.Message; }
    }

    [RelayCommand]
    private async Task AddUserAsync()
    {
        if (string.IsNullOrWhiteSpace(NewFullName) || string.IsNullOrWhiteSpace(NewUsername) || string.IsNullOrWhiteSpace(NewPassword))
        { Error = "Full name, username and password are required."; return; }
        try
        {
            Info = ""; Error = "";
            await api.CreateUserAsync(new CreateUserRequest(NewFullName.Trim(), NewUsername.Trim(), NewPassword, NewRole));
            NewFullName = NewUsername = NewPassword = "";
            NewRole = UserRole.Worker;
            await LoadUsersAsync();
            Info = "User created.";
        }
        catch (Exception ex) { Error = ex.Message; }
    }

    [RelayCommand]
    private async Task SaveUserAsync()
    {
        if (SelectedUser is null) { Error = "Select a user to edit."; return; }
        try
        {
            Info = ""; Error = "";
            var pwd = string.IsNullOrWhiteSpace(EditNewPassword) ? null : EditNewPassword;
            await api.UpdateUserAsync(SelectedUser.Id, new UpdateUserRequest(EditFullName.Trim(), EditRole, EditActive, pwd));
            EditNewPassword = "";
            await LoadUsersAsync();
            Info = "User updated.";
        }
        catch (Exception ex) { Error = ex.Message; }
    }
}
