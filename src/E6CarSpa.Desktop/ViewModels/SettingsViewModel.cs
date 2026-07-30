using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using E6CarSpa.Contracts;
using E6CarSpa.Client;
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
    [ObservableProperty] private string _nonGstInvoicePrefix = "";

    /// <summary>
    /// Live example of the next non-GST number. The prefix field takes ONLY the prefix — the year
    /// and the 4-digit counter are appended automatically — so this preview makes that obvious
    /// (typing the whole format in produced numbers like E6/2026/00002026/0001).
    /// </summary>
    public string NonGstPreview => $"{NonGstInvoicePrefix}{DateTime.Now.Year}/0001";
    partial void OnNonGstInvoicePrefixChanged(string value) => OnPropertyChanged(nameof(NonGstPreview));
    [ObservableProperty] private long _lastNonGstSequence;
    [ObservableProperty] private int _lastNonGstYear;



    // Change my password
    [ObservableProperty] private string _myNewPassword = "";

    // ----- Logo -----
    [ObservableProperty] private byte[]? _logoBytes;
    public bool HasLogo => LogoBytes is { Length: > 0 };
    partial void OnLogoBytesChanged(byte[]? value) => OnPropertyChanged(nameof(HasLogo));

    [ObservableProperty] private string _info = "";
    [ObservableProperty] private string _error = "";

    public string MyUsername => api.CurrentUser?.Username ?? "";

    /// <summary>Only an Admin may manage staff logins; the Users tab is hidden for everyone else.
    /// The API enforces this too — this just avoids showing a tab that would only return 403.</summary>
    public bool IsAdmin => api.CurrentUser?.Role is UserRole.Admin;

    /// <summary>
    /// Raised after the signed-in user changes their own password. The server rotates the security
    /// stamp, so the current token is dead — the view uses this to send the user back to a login.
    /// </summary>
    public event Action? OwnPasswordChanged;

    // ----- Staff users (Admin only) -----
    public ObservableCollection<UserDto> Users { get; } = new();
    public IReadOnlyList<UserRole> Roles { get; } = Enum.GetValues<UserRole>();

    [ObservableProperty] private string _newUserFullName = "";
    [ObservableProperty] private string _newUserUsername = "";
    [ObservableProperty] private string _newUserPassword = "";
    [ObservableProperty] private UserRole _newUserRole = UserRole.Worker;
    [ObservableProperty] private string _userInfo = "";
    [ObservableProperty] private string _userError = "";

    public async Task InitializeAsync()
    {
        await LoadSettingsAsync();
        if (IsAdmin) await LoadUsersAsync();
    }

    [RelayCommand]
    private async Task LoadUsersAsync()
    {
        if (!IsAdmin) return;
        try
        {
            UserError = "";
            var list = await api.GetUsersAsync() ?? new();
            Users.Clear();
            foreach (var u in list) Users.Add(u);
        }
        catch (Exception ex) { UserError = ex.Message; }
    }

    [RelayCommand]
    private async Task CreateUserAsync()
    {
        UserInfo = ""; UserError = "";

        if (string.IsNullOrWhiteSpace(NewUserFullName)) { UserError = "Enter the person's full name."; return; }
        if (string.IsNullOrWhiteSpace(NewUserUsername)) { UserError = "Enter a username to sign in with."; return; }
        if (NewUserPassword.Length < 8) { UserError = "Password must be at least 8 characters."; return; }

        try
        {
            var created = await api.CreateUserAsync(new CreateUserRequest(
                NewUserFullName.Trim(), NewUserUsername.Trim(), NewUserPassword, NewUserRole));

            Users.Add(created);
            UserInfo = $"Created '{created.Username}' ({created.Role}). Tell them their password — it is not shown again.";
            NewUserFullName = ""; NewUserUsername = ""; NewUserPassword = ""; NewUserRole = UserRole.Worker;
        }
        catch (Exception ex) { UserError = ex.Message; }
    }

    /// <summary>Enable/disable a login. Deactivating revokes their token immediately (security stamp).</summary>
    [RelayCommand]
    private async Task ToggleUserActiveAsync(UserDto? user)
    {
        if (user is null) return;
        UserInfo = ""; UserError = "";

        if (user.Id == api.CurrentUser?.Id)
        {
            UserError = "You cannot deactivate the account you are signed in with.";
            return;
        }

        try
        {
            await api.UpdateUserAsync(user.Id,
                new UpdateUserRequest(user.FullName, user.Role, !user.IsActive, null));
            UserInfo = user.IsActive ? $"'{user.Username}' deactivated." : $"'{user.Username}' reactivated.";
            await LoadUsersAsync();
        }
        catch (Exception ex) { UserError = ex.Message; }
    }

    /// <summary>Set a new password for someone who has forgotten theirs. Also clears their lockout.</summary>
    public async Task ResetUserPasswordAsync(UserDto user, string newPassword)
    {
        UserInfo = ""; UserError = "";
        if (newPassword.Length < 8) { UserError = "Password must be at least 8 characters."; return; }

        try
        {
            await api.UpdateUserAsync(user.Id,
                new UpdateUserRequest(user.FullName, user.Role, user.IsActive, newPassword));
            UserInfo = $"Password reset for '{user.Username}'. They are signed out of any open session.";
        }
        catch (Exception ex) { UserError = ex.Message; }
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
            NonGstInvoicePrefix = s.NonGstInvoicePrefix; LastNonGstSequence = s.LastNonGstSequence; LastNonGstYear = s.LastNonGstYear;
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



    [RelayCommand]
    private async Task SaveSettingsAsync()
    {
        try
        {
            Info = ""; Error = "";
            var req = new SaveCompanySettingsRequest(Name, AddressLine1, AddressLine2, City, State,
                StateCode, Pincode, Phone, Email, Gstin, InvoicePrefix, NonGstInvoicePrefix, DefaultGstRate);
            await api.UpdateSettingsAsync(req);
            Info = "Company settings saved.";
        }
        catch (Exception ex) { Error = ex.Message; }
    }

    [ObservableProperty] private string myOldPassword = "";

    [RelayCommand]
    private async Task ChangeMyPasswordAsync()
    {
        if (string.IsNullOrWhiteSpace(MyOldPassword))
        { Error = "Please enter your old password."; return; }
        if (string.IsNullOrWhiteSpace(MyNewPassword) || MyNewPassword.Length < 8)
        { Error = "New password must be at least 8 characters."; return; }
        
        try
        {
            Info = ""; Error = "";
            await api.ChangeMyPasswordAsync(new ChangeMyPasswordRequest(MyOldPassword, MyNewPassword));
            MyOldPassword = "";
            MyNewPassword = "";
            Info = "Your password has been changed.";
            // The server revoked this session's token, so the shell must send us back to a login
            // rather than sit there signed out with every later call failing.
            OwnPasswordChanged?.Invoke();
        }
        catch (Exception ex) { Error = ex.Message; }
    }


}
