using E6CarSpa.Client;
using E6CarSpa.Contracts;
using E6CarSpa.Domain.Enums;
using E6CarSpa.Mobile.Services;


namespace E6CarSpa.Mobile.Pages;

/// <summary>
/// Staff logins on the phone — the same accounts and permissions as the desktop Users tab, since
/// both apps talk to one server. Needs the ManageUsers permission; the API refuses otherwise.
/// </summary>
public partial class UsersPage : ContentPage
{
    private static readonly UserRole[] Roles = Enum.GetValues<UserRole>();
    private List<PermissionOption> _permissions = PermissionOption.BuildList(PermissionPresets.For(UserRole.Worker));

    public UsersPage()
    {
        InitializeComponent();

        foreach (var r in Roles) RolePicker.Items.Add(r.ToString());
        RolePicker.SelectedIndex = Array.IndexOf(Roles, UserRole.Worker);
        BindableLayout.SetItemsSource(PermissionsHost, _permissions);
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        ThemeColors.ApplyTo(RolePicker);
        await LoadAsync();
    }

    private async void OnRefreshing(object? sender, EventArgs e)
    {
        await LoadAsync();
        Refresh.IsRefreshing = false;
    }

    /// <summary>Re-tick the list to the chosen role's preset; the admin can still adjust it.</summary>
    private void OnRoleChanged(object? sender, EventArgs e)
    {
        if (RolePicker.SelectedIndex < 0) return;
        var preset = PermissionPresets.For(Roles[RolePicker.SelectedIndex]);
        foreach (var option in _permissions) option.IsGranted = preset.HasFlag(option.Value);
    }

    private async Task LoadAsync()
    {
        ErrorLabel.IsVisible = false;
        try
        {
            UsersList.ItemsSource = await AppServices.Api.GetUsersAsync() ?? new();
        }
        catch (Exception ex)
        {
            ShowError(ex is ApiException a ? a.Message : "Cannot reach the server. Pull down to retry.");
        }
    }

    private async void OnAddUserClicked(object? sender, EventArgs e)
    {
        ErrorLabel.IsVisible = false;
        InfoLabel.IsVisible = false;

        var fullName = FullNameEntry.Text?.Trim() ?? "";
        var username = UsernameEntry.Text?.Trim() ?? "";
        var password = PasswordEntry.Text ?? "";

        if (fullName.Length == 0) { ShowError("Enter the person's full name."); return; }
        if (username.Length == 0) { ShowError("Enter a username to sign in with."); return; }
        if (password.Length < 8) { ShowError("Password must be at least 8 characters."); return; }

        var permissions = PermissionOption.Combine(_permissions);
        if (permissions == Permission.None)
        {
            ShowError("Tick at least one permission, or the account will not be able to open anything.");
            return;
        }

        SetBusy(true);
        try
        {
            var role = Roles[Math.Max(0, RolePicker.SelectedIndex)];
            var created = await AppServices.Api.CreateUserAsync(
                new CreateUserRequest(fullName, username, password, role, permissions));

            InfoLabel.Text = $"Created '{created.Username}'. Tell them their password — it is not shown again.";
            InfoLabel.IsVisible = true;

            FullNameEntry.Text = UsernameEntry.Text = PasswordEntry.Text = "";
            RolePicker.SelectedIndex = Array.IndexOf(Roles, UserRole.Worker);

            await LoadAsync();
        }
        catch (Exception ex)
        {
            ShowError(ex is ApiException a ? a.Message : "Could not create the user.");
        }
        finally { SetBusy(false); }
    }

    /// <summary>Tapping a login offers the actions that need a confirmation step.</summary>
    private async void OnUserSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is not UserDto user) return;
        UsersList.SelectedItem = null;   // clear highlight so re-tapping works

        var choice = await DisplayActionSheetAsync($"{user.FullName} ({user.Username})", "Cancel", null,
            "Edit permissions", user.IsActive ? "Deactivate login" : "Reactivate login", "Reset password");

        switch (choice)
        {
            case "Edit permissions": await EditPermissionsAsync(user); break;
            case "Deactivate login":
            case "Reactivate login": await ToggleActiveAsync(user); break;
            case "Reset password": await ResetPasswordAsync(user); break;
        }
    }

    private async Task EditPermissionsAsync(UserDto user)
    {
        // A phone has no room for a tick-list dialog, so walk the permissions one at a time.
        var current = user.Permissions;
        foreach (var option in PermissionOption.BuildList(current))
        {
            var grant = await DisplayAlertAsync(option.Label, option.Description,
                option.IsGranted ? "Keep allowed" : "Allow",
                option.IsGranted ? "Remove" : "Keep blocked");

            // The left button means "allowed" in both wordings; the right means "not allowed".
            if (grant) current |= option.Value;
            else current &= ~option.Value;
        }

        if (current == Permission.None)
        {
            ShowError("Tick at least one permission, or the account will not be able to open anything.");
            return;
        }

        await UpdateAsync(user, user.IsActive, null, current,
            $"Permissions updated for '{user.Username}'. They will need to sign in again.");
    }

    private async Task ToggleActiveAsync(UserDto user)
    {
        if (user.Id == AppServices.Api.CurrentUser?.Id)
        {
            ShowError("You cannot deactivate the account you are signed in with.");
            return;
        }

        await UpdateAsync(user, !user.IsActive, null, null,
            user.IsActive ? $"'{user.Username}' deactivated." : $"'{user.Username}' reactivated.");
    }

    private async Task ResetPasswordAsync(UserDto user)
    {
        var next = await DisplayPromptAsync($"Reset password for '{user.Username}'",
            "Enter a new password (min 8 characters).", "Set", "Cancel", maxLength: 200);

        if (string.IsNullOrEmpty(next)) return;
        if (next.Length < 8) { ShowError("Password must be at least 8 characters."); return; }

        await UpdateAsync(user, user.IsActive, next, null,
            $"Password reset for '{user.Username}'. They are signed out of any open session.");
    }

    private async Task UpdateAsync(UserDto user, bool isActive, string? newPassword,
        Permission? permissions, string successMessage)
    {
        ErrorLabel.IsVisible = false;
        InfoLabel.IsVisible = false;
        SetBusy(true);
        try
        {
            await AppServices.Api.UpdateUserAsync(user.Id,
                new UpdateUserRequest(user.FullName, user.Role, isActive, newPassword, permissions));

            InfoLabel.Text = successMessage;
            InfoLabel.IsVisible = true;
            await LoadAsync();
        }
        catch (Exception ex)
        {
            ShowError(ex is ApiException a ? a.Message : "Could not update the user.");
        }
        finally { SetBusy(false); }
    }

    private void SetBusy(bool busy)
    {
        Busy.IsRunning = busy;
        Busy.IsVisible = busy;
        AddButton.IsEnabled = !busy;
    }

    private void ShowError(string message)
    {
        ErrorLabel.Text = message;
        ErrorLabel.IsVisible = true;
    }
}
