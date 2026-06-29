using CommunityToolkit.Mvvm.ComponentModel;
using E6CarSpa.Desktop.Services;

namespace E6CarSpa.Desktop.ViewModels;

public partial class LoginViewModel(IApiClient api) : ObservableObject
{
    [ObservableProperty] private string _username = "admin";
    [ObservableProperty] private string _errorMessage = "";
    [ObservableProperty] private bool _isBusy;

    /// <summary>Returns true on success. Password is passed from the PasswordBox in code-behind.</summary>
    public async Task<bool> LoginAsync(string password)
    {
        ErrorMessage = "";
        if (string.IsNullOrWhiteSpace(Username) || string.IsNullOrWhiteSpace(password))
        {
            ErrorMessage = "Enter username and password.";
            return false;
        }

        try
        {
            IsBusy = true;
            await api.LoginAsync(Username.Trim(), password);
            return true;
        }
        catch (ApiException ex)
        {
            ErrorMessage = ex.Message;
            return false;
        }
        catch (Exception)
        {
            ErrorMessage = "Cannot reach the server. Is the API running?";
            return false;
        }
        finally
        {
            IsBusy = false;
        }
    }
}
