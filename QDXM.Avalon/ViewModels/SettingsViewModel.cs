using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using QobuzApiSharp.Models.User;
using QDXM.Avalon.Core.Protocol;
using QDXM.Avalon.Core.Settings;
using QDXM.Avalon.Core.Tools;

namespace QDXM.Avalon.ViewModels;

public partial class SettingsViewModel : ViewModelBase
{
    private const int MinFileNameLength = 1;
    private const int MaxFileNameLength = 100;
    private readonly ISettingsStore settingsStore;

    public SettingsViewModel()
        : this(new JsonSettingsStore())
    {
    }

    public SettingsViewModel(ISettingsStore settingsStore)
    {
        this.settingsStore = settingsStore;
        Settings = settingsStore.Current;
        Settings.CoverArtSize = CoverArtUrlSelector.GetArtSizeDisplayName(Settings.CoverArtSize);
        Settings.Tagging.ArtSize = CoverArtUrlSelector.GetArtSizeDisplayName(Settings.Tagging.ArtSize);
        SelectedQuality = GetSelectedQuality(Settings);
        DownloadFolder = Settings.DownloadFolder;
        MaxFileNameLengthValue = Settings.MaxFileNameLength;
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
