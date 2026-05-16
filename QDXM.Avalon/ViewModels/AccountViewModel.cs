using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using QobuzApiSharp.Models.User;
using QDXM.Avalon.Core.Settings;

namespace QDXM.Avalon.ViewModels;

public partial class AccountViewModel : ViewModelBase
{
    [ObservableProperty]
    private string displayName = "Not logged in";

    [ObservableProperty]
    private bool isLoggedIn;

    [ObservableProperty]
    private string actionText = "Log in";

    public AccountViewModel()
    {
    }

    public AccountViewModel(AppSettings settings)
    {
        UpdateFromSettings(settings);
    }

    public event EventHandler? LogoutRequested;
    public event EventHandler? LoginRequested;

    public void UpdateFromSettings(AppSettings settings)
    {
        IsLoggedIn = !string.IsNullOrWhiteSpace(settings.UserAuthToken);
        DisplayName = IsLoggedIn
            ? "Loading..."
            : "Not logged in";
        ActionText = IsLoggedIn ? "Log out" : "Log in";
    }

    public void UpdateFromLogin(Login login, AppSettings settings)
    {
        IsLoggedIn = !string.IsNullOrWhiteSpace(settings.UserAuthToken);
        DisplayName = IsLoggedIn
            ? GetAccountName(login, settings)
            : "Not logged in";
        ActionText = IsLoggedIn ? "Log out" : "Log in";
    }

    [RelayCommand]
    private void ToggleLogin()
    {
        if (!IsLoggedIn)
        {
            LoginRequested?.Invoke(this, EventArgs.Empty);
            return;
        }

        DisplayName = "Not logged in";
        IsLoggedIn = false;
        ActionText = "Log in";
        LogoutRequested?.Invoke(this, EventArgs.Empty);
    }

    private static string GetAccountName(Login login, AppSettings settings)
    {
        if (!string.IsNullOrWhiteSpace(login.User?.DisplayName))
        {
            return login.User.DisplayName;
        }

        return "Qobuz user";
    }
}
