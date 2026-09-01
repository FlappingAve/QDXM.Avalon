using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using QobuzApiSharp.Exceptions;
using QobuzApiSharp.Models.User;
using QDXM.Avalon.Core.Api;
using QDXM.Avalon.Core.Auth;
using QDXM.Avalon.Core.Protocol;
using QDXM.Avalon.Core.Settings;
using QDXM.Avalon.Core.Tools;
using QDXM.Avalon.Services;

namespace QDXM.Avalon.ViewModels;

public partial class SettingsViewModel : ViewModelBase
{
    private const int MinFileNameLength = 1;
    private const int MaxFileNameLength = 100;
    private readonly ISettingsStore settingsStore;
    private readonly IUserCredentialStore previewCredentialStore;
    private readonly AppLogService logService;
    private readonly QobuzBrowserLoginService browserLoginService;
    private CancellationTokenSource? previewBrowserLoginCancellation;

    public SettingsViewModel()
        : this(new JsonSettingsStore())
    {
    }

    public SettingsViewModel(ISettingsStore settingsStore)
        : this(settingsStore, NullUserCredentialStore.Instance, new AppLogService(), new QobuzBrowserLoginService())
    {
    }

    public SettingsViewModel(
        ISettingsStore settingsStore,
        IUserCredentialStore previewCredentialStore,
        AppLogService? logService = null,
        QobuzBrowserLoginService? browserLoginService = null)
    {
        this.settingsStore = settingsStore;
        this.previewCredentialStore = previewCredentialStore;
        this.logService = logService ?? new AppLogService();
        this.browserLoginService = browserLoginService ?? new QobuzBrowserLoginService();
        Settings = settingsStore.Current;
        Settings.CoverArtSize = CoverArtUrlSelector.GetArtSizeDisplayName(Settings.CoverArtSize);
        Settings.Tagging.ArtSize = CoverArtUrlSelector.GetArtSizeDisplayName(Settings.Tagging.ArtSize);
        SelectedQuality = GetSelectedQuality(Settings);
        DownloadFolder = Settings.DownloadFolder;
        MaxFileNameLengthValue = Settings.MaxFileNameLength;
        _ = LoadPreviewCredentialAsync();
    }

    public event EventHandler<AppSettings>? SettingsSaved;

    public AppSettings Settings { get; }
    public IReadOnlyList<string> QualityOptions { get; } =
        [QualityStringMappings.FlacHighestLabel, QualityStringMappings.Mp3Label];

    public IReadOnlyList<string> DuplicateFileOptions { get; } =
        [AppSettings.DuplicateFileSkip, AppSettings.DuplicateFileOverwrite, AppSettings.DuplicateFileKeepBoth];

    public IReadOnlyList<string> ArtSizeOptions { get; } =
    [
        CoverArtUrlSelector.OriginalDisplayName,
        CoverArtUrlSelector.MaxDisplayName,
        CoverArtUrlSelector.RecommendedDisplayName,
        "300 px",
        "230 px",
        "150 px",
        "100 px",
        "50 px"
    ];

    [ObservableProperty]
    private string selectedQuality;

    [ObservableProperty]
    private string downloadFolder = string.Empty;

    [ObservableProperty]
    private decimal? maxFileNameLengthValue;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasMaxFileNameLengthError))]
    private string maxFileNameLengthErrorText = string.Empty;

    [ObservableProperty]
    private string statusText = "Settings ready";

    [ObservableProperty]
    private string accountName = "Loading...";

    [ObservableProperty]
    private string accountEmail = "Loading...";

    [ObservableProperty]
    private string accountZone = "Loading...";

    [ObservableProperty]
    private string accountCredential = "Loading...";

    [ObservableProperty]
    private string accountSubscription = "Loading account details...";

    [ObservableProperty]
    private string previewUserId = string.Empty;

    [ObservableProperty]
    private string previewUserAuthToken = string.Empty;

    [ObservableProperty]
    private string previewAppId = string.Empty;

    [ObservableProperty]
    private string previewAppSecret = string.Empty;

    [ObservableProperty]
    private string previewCredentialStatus = "No preview account saved.";

    [ObservableProperty]
    private string previewAccountName = string.Empty;

    [ObservableProperty]
    private string previewAccountEmail = string.Empty;

    [ObservableProperty]
    private string previewAccountZone = string.Empty;

    [ObservableProperty]
    private string previewAccountCredential = string.Empty;

    [ObservableProperty]
    private string previewAccountSubscription = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsPreviewLoggedOut))]
    private bool isPreviewLoggedIn;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsPreviewNotBusy))]
    private bool isPreviewBusy;

    [ObservableProperty]
    private bool isPreviewBrowserLoginActive;

    public bool IsPreviewLoggedOut => !IsPreviewLoggedIn;
    public bool IsPreviewNotBusy => !IsPreviewBusy;

    public string AppIdDisplay => HasManualApiCredentials
        ? Settings.AppId
        : "Using dynamic web-player value";

    public string AppSecretDisplay => HasManualApiCredentials
        ? Settings.AppSecret
        : "Using dynamic web-player value";

    private bool HasManualApiCredentials =>
        !string.IsNullOrWhiteSpace(Settings.AppId) &&
        !string.IsNullOrWhiteSpace(Settings.AppSecret);

    public string ProtocolStatusText => ProtocolHandler.IsProtocolRegistered()
        ? $"{ProtocolHandler.ProtocolName}:// is registered"
        : $"{ProtocolHandler.ProtocolName}:// is not registered";

    public string MaxFileNameLengthHelpText => $"Range: {MinFileNameLength}-{MaxFileNameLength}.";

    public bool HasMaxFileNameLengthError => !string.IsNullOrWhiteSpace(MaxFileNameLengthErrorText);

    public void RefreshFromSettings()
    {
        SelectedQuality = GetSelectedQuality(Settings);
        DownloadFolder = Settings.DownloadFolder;
        MaxFileNameLengthValue = Settings.MaxFileNameLength;

        OnPropertyChanged(nameof(AppIdDisplay));
        OnPropertyChanged(nameof(AppSecretDisplay));
    }

    public void UpdateAccountInfo(Login login)
    {
        var user = login.User;
        if (user is null)
        {
            return;
        }

        AccountName = DisplayValue(user.DisplayName);
        AccountEmail = DisplayValue(user.Email);
        AccountZone = DisplayValue(user.Zone);
        AccountCredential = DisplayValue(user.Credential?.Description);
        AccountSubscription = GetSubscriptionText(user);
    }

    private static string GetSubscriptionText(User user)
    {
        if (user.Subscription is not null)
        {
            return string.Join(Environment.NewLine,
                $"Offer Type - {DisplayValue(user.Subscription.Offer)}",
                $"Start Date - {DisplayValue(StringTools.FormatDateTimeOffset(user.Subscription.StartDate))}",
                $"End Date - {DisplayValue(StringTools.FormatDateTimeOffset(user.Subscription.EndDate))}",
                $"Periodicity - {DisplayValue(user.Subscription.Periodicity)}");
        }

        if (user.Credential?.Parameters?.Source == "household" &&
            user.Credential.Parameters.HiresStreaming == true)
        {
            return string.Join(Environment.NewLine,
                "Active Family sub-account, unknown End Date",
                $"Credential Label - {DisplayValue(user.Credential.Label)}");
        }

        return "No active subscriptions, only sample downloads possible.";
    }

    private static string DisplayValue(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? "?" : value;
    }

    private static string GetSelectedQuality(AppSettings settings)
    {
        var label = QualityStringMappings.GetQualityLabelFromFormatId(settings.FormatId);
        return string.IsNullOrWhiteSpace(label)
            ? QualityStringMappings.FlacHighestLabel
            : label;
    }


    partial void OnMaxFileNameLengthValueChanged(decimal? value)
    {
        ValidateMaxFileNameLength(value);
    }

    [RelayCommand]
    private async Task Save()
    {
        if (!TryGetMaxFileNameLength(out var maxFileNameLength))
        {
            StatusText = MaxFileNameLengthErrorText;
            return;
        }

        Settings.SelectedQuality = SelectedQuality;
        Settings.DownloadFolder = DownloadFolder;
        Settings.MaxFileNameLength = maxFileNameLength;
        var formatId = QualityStringMappings.GetFormatIdFromQualityLabel(SelectedQuality);
        if (!string.IsNullOrWhiteSpace(formatId))
        {
            Settings.FormatId = formatId;
        }

        await settingsStore.SaveAsync(Settings);
        SettingsSaved?.Invoke(this, Settings);
        OnPropertyChanged(nameof(AppIdDisplay));
        OnPropertyChanged(nameof(AppSecretDisplay));
        StatusText = "Settings saved";
    }

    [RelayCommand]
    private async Task BrowserPreviewLogin()
    {
        if (IsPreviewBusy)
        {
            return;
        }

        previewBrowserLoginCancellation?.Dispose();
        previewBrowserLoginCancellation = new CancellationTokenSource();
        IsPreviewBusy = true;
        IsPreviewBrowserLoginActive = true;
        PreviewCredentialStatus = "Opening Qobuz in your browser...";

        try
        {
            var result = await browserLoginService.LoginAsync(
                TimeSpan.FromMinutes(5),
                previewBrowserLoginCancellation.Token);
            var login = result.Login;
            var userId = login.User?.Id?.ToString(CultureInfo.InvariantCulture);
            if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(login.AuthToken))
            {
                PreviewCredentialStatus = "Browser login succeeded, but Qobuz did not return a usable user id and auth token.";
                return;
            }

            PreviewUserId = userId;
            PreviewUserAuthToken = login.AuthToken;
            PreviewAppId = result.AppId;
            PreviewAppSecret = result.AppSecret;
            await SavePreviewCredentialAsync(login).ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            PreviewCredentialStatus = previewBrowserLoginCancellation?.IsCancellationRequested == true
                ? "Preview browser login canceled."
                : "Preview browser login timed out.";
        }
        catch (ApiErrorResponseException ex)
        {
            PreviewCredentialStatus = SafeErrorText.FormatApiFailure("Preview browser login", ex);
            logService.Error("Preview browser login", PreviewCredentialStatus);
        }
        catch (Exception ex)
        {
            PreviewCredentialStatus = SafeErrorText.FormatUnexpectedFailure("Preview browser login");
            logService.Error("Preview browser login", SafeErrorText.FormatUnexpectedLogMessage(ex));
        }
        finally
        {
            previewBrowserLoginCancellation?.Dispose();
            previewBrowserLoginCancellation = null;
            IsPreviewBrowserLoginActive = false;
            IsPreviewBusy = false;
        }
    }

    [RelayCommand]
    private void CancelPreviewBrowserLogin()
    {
        previewBrowserLoginCancellation?.Cancel();
    }

    [RelayCommand]
    private async Task SavePreviewAccount()
    {
        if (IsPreviewBusy)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(PreviewUserId) ||
            string.IsNullOrWhiteSpace(PreviewUserAuthToken))
        {
            PreviewCredentialStatus = "Preview user id and auth token are required.";
            return;
        }

        IsPreviewBusy = true;
        PreviewCredentialStatus = "Checking preview account...";

        try
        {
            var login = await Task.Run(() =>
            {
                using var service = new QobuzApiServiceFactory(
                    PreviewAppId.Trim(),
                    PreviewAppSecret.Trim()).Create();
                return service.LoginWithToken(PreviewUserId.Trim(), PreviewUserAuthToken.Trim());
            }).ConfigureAwait(true);

            await SavePreviewCredentialAsync(login).ConfigureAwait(true);
        }
        catch (ApiErrorResponseException ex)
        {
            PreviewCredentialStatus = SafeErrorText.FormatApiFailure("Preview login", ex);
            logService.Error("Preview login", PreviewCredentialStatus);
        }
        catch (Exception ex)
        {
            PreviewCredentialStatus = SafeErrorText.FormatUnexpectedFailure("Preview login");
            logService.Error("Preview login", SafeErrorText.FormatUnexpectedLogMessage(ex));
        }
        finally
        {
            IsPreviewBusy = false;
        }
    }

    [RelayCommand]
    private async Task ClearPreviewAccount()
    {
        if (IsPreviewBusy)
        {
            return;
        }

        IsPreviewBusy = true;
        try
        {
            previewBrowserLoginCancellation?.Cancel();
            await previewCredentialStore.DeleteAsync().ConfigureAwait(true);
            ClearPreviewAccountFields();
            PreviewCredentialStatus = "Preview account logged out.";
            StatusText = "Preview account cleared";
        }
        finally
        {
            IsPreviewBusy = false;
        }
    }

    [RelayCommand]
    private void RegisterProtocol()
    {
        ProtocolHandler.RegisterProtocol();
        OnPropertyChanged(nameof(ProtocolStatusText));
        StatusText = ProtocolHandler.IsProtocolRegistered()
            ? "Protocol registered"
            : "Protocol registration unavailable";
    }

    [RelayCommand]
    private void UnregisterProtocol()
    {
        ProtocolHandler.UnregisterProtocol();
        OnPropertyChanged(nameof(ProtocolStatusText));
        StatusText = ProtocolHandler.IsProtocolRegistered()
            ? "Protocol unregister failed"
            : "Protocol unregistered";
    }

    private bool TryGetMaxFileNameLength(out int value)
    {
        return ValidateMaxFileNameLength(MaxFileNameLengthValue, out value);
    }

    private async Task LoadPreviewCredentialAsync()
    {
        try
        {
            var credential = await previewCredentialStore.ReadAsync().ConfigureAwait(true);
            if (credential is null)
            {
                ClearPreviewAccountFields();
                return;
            }

            PreviewUserId = credential.UserId;
            PreviewUserAuthToken = credential.UserAuthToken;
            PreviewAppId = credential.AppId;
            PreviewAppSecret = credential.AppSecret;
            if (string.IsNullOrWhiteSpace(credential.UserAuthToken))
            {
                ClearPreviewAccountFields();
                return;
            }

            IsPreviewLoggedIn = true;
            PreviewCredentialStatus = "Refreshing preview account...";

            var login = await Task.Run(() =>
            {
                using var service = new QobuzApiServiceFactory(
                    credential.AppId,
                    credential.AppSecret,
                    credential.UserAuthToken).Create();
                return service.LoginWithToken(credential.UserId, credential.UserAuthToken);
            }).ConfigureAwait(true);

            UpdatePreviewAccountInfo(login);
            PreviewCredentialStatus = "Preview account saved.";
        }
        catch (Exception ex)
        {
            if (!string.IsNullOrWhiteSpace(PreviewUserAuthToken))
            {
                IsPreviewLoggedIn = true;
                PreviewAccountName = DisplayValue(PreviewUserId);
                PreviewAccountEmail = "?";
                PreviewAccountZone = "?";
                PreviewAccountCredential = "?";
                PreviewAccountSubscription = "?";
                PreviewCredentialStatus = "Preview account saved, but account details could not be refreshed.";
            }
            else
            {
                PreviewCredentialStatus = SafeErrorText.FormatUnexpectedFailure("Load preview account");
            }

            logService.Warning("Preview account", SafeErrorText.FormatUnexpectedLogMessage(ex));
        }
    }

    private async Task SavePreviewCredentialAsync(Login login)
    {
        await previewCredentialStore.SaveAsync(new UserCredential(
            PreviewUserId.Trim(),
            login.AuthToken,
            PreviewAppId.Trim(),
            PreviewAppSecret.Trim())).ConfigureAwait(true);

        PreviewUserAuthToken = login.AuthToken;
        UpdatePreviewAccountInfo(login);
        PreviewCredentialStatus = $"Preview account saved for {GetDisplayName(login)}.";
        StatusText = "Preview account saved";
    }

    private void UpdatePreviewAccountInfo(Login login)
    {
        var user = login.User;
        IsPreviewLoggedIn = true;
        PreviewAccountName = DisplayValue(user?.DisplayName);
        PreviewAccountEmail = DisplayValue(user?.Email);
        PreviewAccountZone = DisplayValue(user?.Zone);
        PreviewAccountCredential = DisplayValue(user?.Credential?.Description);
        PreviewAccountSubscription = user is null ? "?" : GetSubscriptionText(user);
    }

    private void ClearPreviewAccountFields()
    {
        PreviewUserId = string.Empty;
        PreviewUserAuthToken = string.Empty;
        PreviewAppId = string.Empty;
        PreviewAppSecret = string.Empty;
        PreviewAccountName = string.Empty;
        PreviewAccountEmail = string.Empty;
        PreviewAccountZone = string.Empty;
        PreviewAccountCredential = string.Empty;
        PreviewAccountSubscription = string.Empty;
        PreviewCredentialStatus = "No preview account saved.";
        IsPreviewLoggedIn = false;
    }

    private static string GetDisplayName(Login login)
    {
        return !string.IsNullOrWhiteSpace(login.User?.DisplayName)
            ? login.User.DisplayName
            : "Qobuz user";
    }

    private sealed class NullUserCredentialStore : IUserCredentialStore
    {
        public static readonly NullUserCredentialStore Instance = new();

        public Task<UserCredential?> ReadAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult<UserCredential?>(null);
        }

        public Task SaveAsync(UserCredential credential, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task DeleteAsync(CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }

    private bool ValidateMaxFileNameLength(decimal? candidate)
    {
        return ValidateMaxFileNameLength(candidate, out _);
    }

    private bool ValidateMaxFileNameLength(decimal? candidate, out int value)
    {
        value = 0;

        if (candidate is null)
        {
            MaxFileNameLengthErrorText = $"Enter a file name length from {MinFileNameLength} to {MaxFileNameLength}.";
            return false;
        }

        var numericValue = candidate.Value;
        if (numericValue != decimal.Truncate(numericValue))
        {
            MaxFileNameLengthErrorText = "Use a whole number for max file name length.";
            return false;
        }

        if (numericValue < MinFileNameLength || numericValue > MaxFileNameLength)
        {
            MaxFileNameLengthErrorText = $"Max file name length must be between {MinFileNameLength} and {MaxFileNameLength}.";
            return false;
        }

        value = (int)numericValue;
        MaxFileNameLengthErrorText = string.Empty;
        return true;
    }
}
