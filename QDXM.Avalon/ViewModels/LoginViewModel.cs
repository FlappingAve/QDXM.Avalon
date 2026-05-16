using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using QobuzApiSharp.Exceptions;
using QobuzApiSharp.Models.User;
using QDXM.Avalon.Core.Api;
using QDXM.Avalon.Core.Auth;
using QDXM.Avalon.Core.Settings;
using QDXM.Avalon.Services;

namespace QDXM.Avalon.ViewModels;

public partial class LoginViewModel : ViewModelBase
{
    private readonly ISettingsStore settingsStore;
    private readonly AppLogService logService;
    private readonly QobuzBrowserLoginService browserLoginService;
    private CancellationTokenSource? browserLoginCancellation;

    public LoginViewModel()
        : this(new JsonSettingsStore(), new AppLogService(), new QobuzBrowserLoginService())
    {
    }

    public LoginViewModel(
        ISettingsStore settingsStore,
        AppLogService? logService = null,
        QobuzBrowserLoginService? browserLoginService = null)
    {
        this.settingsStore = settingsStore;
        this.logService = logService ?? new AppLogService();
        this.browserLoginService = browserLoginService ?? new QobuzBrowserLoginService();
        UserId = settingsStore.Current.UserId;
        UserAuthToken = settingsStore.Current.UserAuthToken;
        AppId = settingsStore.Current.AppId;
        AppSecret = settingsStore.Current.AppSecret;
    }

    public event EventHandler<LoginSucceededEventArgs>? LoginSucceeded;

    public void ResetAfterLogout()
    {
        UserId = string.Empty;
        UserAuthToken = string.Empty;
        AppId = settingsStore.Current.AppId;
        AppSecret = settingsStore.Current.AppSecret;
        StatusText = "Sign in with your browser or enter your Qobuz user id and auth token manually.";
    }

    [ObservableProperty]
    private string userId = string.Empty;

    [ObservableProperty]
    private string userAuthToken = string.Empty;

    [ObservableProperty]
    private string appId = string.Empty;

    [ObservableProperty]
    private string appSecret = string.Empty;

    [ObservableProperty]
    private string statusText = "Sign in with your browser or enter your Qobuz user id and auth token manually.";

    [ObservableProperty]
    private bool isBusy;

    public bool IsNotBusy => !IsBusy;

    [ObservableProperty]
    private bool isBrowserLoginActive;

    partial void OnIsBusyChanged(bool value)
    {
        OnPropertyChanged(nameof(IsNotBusy));
    }

    [RelayCommand]
    private async Task BrowserLogin()
    {
        if (IsBusy)
        {
            return;
        }

        browserLoginCancellation?.Dispose();
        browserLoginCancellation = new CancellationTokenSource();
        IsBusy = true;
        IsBrowserLoginActive = true;
        StatusText = "Opening Qobuz in your browser...";

        try
        {
            var result = await browserLoginService.LoginAsync(
                TimeSpan.FromMinutes(5),
                browserLoginCancellation.Token);

            var login = result.Login;
            var userId = login.User?.Id?.ToString(CultureInfo.InvariantCulture);
            if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(login.AuthToken))
            {
                StatusText = "Browser login succeeded, but Qobuz did not return a usable user id and auth token.";
                return;
            }

            AppId = string.Empty;
            AppSecret = string.Empty;
            UserId = userId;
            UserAuthToken = login.AuthToken;

            await SaveSuccessfulLoginAsync(login);
        }
        catch (OperationCanceledException)
        {
            StatusText = browserLoginCancellation?.IsCancellationRequested == true
                ? "Browser login canceled."
                : "Browser login timed out.";
        }
        catch (ApiErrorResponseException ex)
        {
            StatusText = SafeErrorText.FormatApiFailure("Browser login", ex);
            logService.Error("Browser login", StatusText);
        }
        catch (Exception ex)
        {
            StatusText = SafeErrorText.FormatUnexpectedFailure("Browser login");
            logService.Error("Browser login", SafeErrorText.FormatUnexpectedLogMessage(ex));
        }
        finally
        {
            browserLoginCancellation?.Dispose();
            browserLoginCancellation = null;
            IsBrowserLoginActive = false;
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void CancelBrowserLogin()
    {
        browserLoginCancellation?.Cancel();
    }

    [RelayCommand]
    private async Task Login()
    {
        if (IsBusy)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(UserId) || string.IsNullOrWhiteSpace(UserAuthToken))
        {
            StatusText = "User ID and auth token are required.";
            return;
        }

        IsBusy = true;
        StatusText = "Checking Qobuz credentials...";

        try
        {
            var login = await Task.Run(() => ValidateLogin(), CancellationToken.None);
            await SaveSuccessfulLoginAsync(login);
        }
        catch (ApiErrorResponseException ex)
        {
            StatusText = SafeErrorText.FormatApiFailure("Login", ex);
            logService.Error("Login", StatusText);
        }
        catch (Exception ex)
        {
            StatusText = SafeErrorText.FormatUnexpectedFailure("Login");
            logService.Error("Login", SafeErrorText.FormatUnexpectedLogMessage(ex));
        }
        finally
        {
            IsBusy = false;
        }
    }

    private Login ValidateLogin()
    {
        using var service = new QobuzApiServiceFactory(AppId.Trim(), AppSecret.Trim()).Create();
        return service.LoginWithToken(UserId.Trim(), UserAuthToken.Trim());
    }

    private async Task SaveSuccessfulLoginAsync(Login login)
    {
        var settings = settingsStore.Current;
        settings.AppId = AppId.Trim();
        settings.AppSecret = AppSecret.Trim();
        settings.UserId = UserId.Trim();
        settings.UserAuthToken = login.AuthToken;

        await settingsStore.SaveAsync(settings);
        StatusText = $"Logged in as {GetDisplayName(login)}.";
        LoginSucceeded?.Invoke(this, new LoginSucceededEventArgs(settings, login));
    }

    private static string GetDisplayName(Login login)
    {
        return !string.IsNullOrWhiteSpace(login.User?.DisplayName)
            ? login.User.DisplayName
            : "Qobuz user";
    }
}

public sealed record LoginSucceededEventArgs(AppSettings Settings, Login Login);
