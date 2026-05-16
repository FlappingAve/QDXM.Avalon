using System.Reflection;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using QobuzApiSharp.Exceptions;
using QDXM.Avalon.Services;
using QDXM.Avalon.Core.Api;
using QDXM.Avalon.Core.Downloads;
using QDXM.Avalon.Core.Settings;

namespace QDXM.Avalon.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    [ObservableProperty]
    private AppPage selectedPage = AppPage.Login;

    public MainWindowViewModel()
        : this(new JsonSettingsStore())
    {
    }

    public MainWindowViewModel(ISettingsStore settingsStore)
    {
        LogService = new AppLogService();
        if (string.IsNullOrWhiteSpace(settingsStore.Current.AppId) ||
            string.IsNullOrWhiteSpace(settingsStore.Current.AppSecret))
        {
            _ = Task.Run(QobuzApiServiceFactory.PrimeDynamicCredentials);
        }
        _ = PrimeStorefrontSearchConfigAsync();

        Downloads = new DownloadsViewModel(CreateDownloadJobRunner(settingsStore.Current), LogService, settings: settingsStore.Current);
        Search = new SearchViewModel(
            CreateQobuzClient(settingsStore.Current),
            EnqueueDownloadFromSearch,
            request => Downloads.EnqueuePartialAlbumRequest(request),
            request => Downloads.EnqueuePartialPlaylistRequest(request),
            LogService,
            settingsStore.Current);
        Tags = new TagsViewModel(settingsStore);
        Settings = new SettingsViewModel(settingsStore);
        Login = new LoginViewModel(settingsStore, LogService);
        Logs = new LogsViewModel(LogService);
        Account = new AccountViewModel(settingsStore.Current);
        selectedPage = Account.IsLoggedIn ? AppPage.Downloads : AppPage.Login;
        Account.LoginRequested += (_, _) => SelectedPage = AppPage.Login;
        Account.LogoutRequested += async (_, _) =>
        {
            settingsStore.Current.UserId = string.Empty;
            settingsStore.Current.UserAuthToken = string.Empty;
            await settingsStore.SaveAsync(settingsStore.Current);
            Search.SetQobuzClient(CreateQobuzClient(settingsStore.Current));
            Downloads.SetDownloadJobRunner(CreateDownloadJobRunner(settingsStore.Current));
            Login.ResetAfterLogout();
            SelectedPage = AppPage.Login;
            OnPropertyChanged(nameof(FooterStatus));
        };

        Login.LoginSucceeded += (_, args) =>
        {
            var settings = args.Settings;
            Search.SetQobuzClient(CreateQobuzClient(settings));
            Downloads.SetDownloadJobRunner(CreateDownloadJobRunner(settings));
            Tags.RefreshFromSettings();
            Settings.RefreshFromSettings();
            Account.UpdateFromLogin(args.Login, settings);
            Settings.UpdateAccountInfo(args.Login);
            SelectedPage = AppPage.Downloads;
            OnPropertyChanged(nameof(FooterStatus));
        };

        Settings.SettingsSaved += (_, settings) =>
            ApplySavedSettings(settings);

        Tags.SettingsSaved += (_, settings) =>
            ApplySavedSettings(settings);

        void ApplySavedSettings(AppSettings settings)
        {
            Search.SetQobuzClient(CreateQobuzClient(settings));
            Downloads.SetDownloadJobRunner(CreateDownloadJobRunner(settings));
            Login.UserId = settings.UserId;
            Login.UserAuthToken = settings.UserAuthToken;
            Login.AppId = settings.AppId;
            Login.AppSecret = settings.AppSecret;
            Search.RefreshSettingsPreview();
            OnPropertyChanged(nameof(FooterStatus));
        }

        Downloads.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(DownloadsViewModel.GlobalStatusText))
            {
                OnPropertyChanged(nameof(FooterStatus));
            }
        };

        Search.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(SearchViewModel.StatusText))
            {
                OnPropertyChanged(nameof(FooterStatus));
            }
        };

        Tags.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(TagsViewModel.StatusText))
            {
                OnPropertyChanged(nameof(FooterStatus));
            }
        };

        Login.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(LoginViewModel.StatusText))
            {
                OnPropertyChanged(nameof(FooterStatus));
            }
        };

        if (Account.IsLoggedIn)
        {
            _ = RefreshAccountProfileAsync(settingsStore);
        }
    }

    public DownloadsViewModel Downloads { get; }
    public SearchViewModel Search { get; }
    public TagsViewModel Tags { get; }
    public SettingsViewModel Settings { get; }
    public LoginViewModel Login { get; }
    public LogsViewModel Logs { get; }
    public AccountViewModel Account { get; }
    public AppLogService LogService { get; }

    public string VersionText { get; } =
        Assembly.GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()!
            .InformationalVersion;

    public ViewModelBase CurrentPage => SelectedPage switch
    {
        AppPage.Login => Login,
        AppPage.Search => Search,
        AppPage.Tags => Tags,
        AppPage.Settings => Settings,
        AppPage.Logs => Logs,
        _ => Downloads
    };

    public bool IsDownloadsSelected => SelectedPage == AppPage.Downloads;
    public bool IsSearchSelected => SelectedPage == AppPage.Search;
    public bool IsTagsSelected => SelectedPage == AppPage.Tags;
    public bool IsSettingsSelected => SelectedPage == AppPage.Settings;
    public bool IsLogsSelected => SelectedPage == AppPage.Logs;
    public bool IsLoginSelected => SelectedPage == AppPage.Login;
    public bool IsShellVisible => SelectedPage != AppPage.Login;

    public string FooterLabel => SelectedPage == AppPage.Search
        ? "Search ready"
        : SelectedPage == AppPage.Login
            ? "Account"
        : "Overall Progress";

    public string FooterStatus => SelectedPage switch
    {
        AppPage.Login => Login.StatusText,
        AppPage.Search => Search.StatusText,
        AppPage.Tags => Tags.StatusText,
        AppPage.Settings => "Settings",
        AppPage.Logs => Logs.EntryCountText,
        _ => Downloads.GlobalStatusText
    };

    [RelayCommand]
    private void ShowDownloads() => SelectedPage = Account.IsLoggedIn ? AppPage.Downloads : AppPage.Login;

    [RelayCommand]
    private void ShowSearch() => SelectedPage = Account.IsLoggedIn ? AppPage.Search : AppPage.Login;

    [RelayCommand]
    private void ShowTags() => SelectedPage = Account.IsLoggedIn ? AppPage.Tags : AppPage.Login;

    [RelayCommand]
    private void ShowSettings() => SelectedPage = Account.IsLoggedIn ? AppPage.Settings : AppPage.Login;

    [RelayCommand]
    private void ShowLogs() => SelectedPage = Account.IsLoggedIn ? AppPage.Logs : AppPage.Login;

    private void EnqueueDownloadFromSearch(DownloadRequest request, SearchResultViewModel result)
    {
        Downloads.EnqueueDownloadRequest(
            request,
            result.Title,
            result.Artist,
            result.Quality,
            result.TotalTracks,
            result.ThumbnailUrl,
            result.ReleaseDate);
    }

    public void EnqueueExternalUrl(string url)
    {
        if (!Account.IsLoggedIn)
        {
            SelectedPage = AppPage.Login;
            Login.StatusText = "Log in before starting downloads from Qobuz links.";
            return;
        }

        SelectedPage = AppPage.Downloads;
        Downloads.TryEnqueueUrl(url);
    }

    private static QobuzClient CreateQobuzClient(AppSettings settings)
    {
        return new QobuzClient(new QobuzApiServiceFactory(
            settings.AppId,
            settings.AppSecret,
            settings.UserAuthToken),
            SharedStorefrontSearchConfigProvider);
    }

    private static QobuzDownloadJobRunner CreateDownloadJobRunner(AppSettings settings)
    {
        return new QobuzDownloadJobRunner(
            new QobuzApiServiceFactory(settings.AppId, settings.AppSecret, settings.UserAuthToken),
            settings);
    }

    private static QobuzStorefrontSearchConfigProvider SharedStorefrontSearchConfigProvider { get; } = new();

    private async Task PrimeStorefrontSearchConfigAsync()
    {
        try
        {
            var config = await SharedStorefrontSearchConfigProvider.GetConfigAsync().ConfigureAwait(true);
            LogService.Info(
                "Search",
                $"Storefront label search config loaded from Qobuz public search page; label index: {config.LabelsIndex}.");
        }
        catch (Exception ex)
        {
            LogService.Warning(
                "Search",
                $"Storefront label search config could not be loaded at startup. Label search will retry on demand. {SafeErrorText.FormatUnexpectedLogMessage(ex)}");
        }
    }

    private async Task RefreshAccountProfileAsync(ISettingsStore settingsStore)
    {
        var settings = settingsStore.Current;
        if (string.IsNullOrWhiteSpace(settings.UserId) ||
            string.IsNullOrWhiteSpace(settings.UserAuthToken))
        {
            return;
        }

        try
        {
            var login = await Task.Run(() =>
            {
                using var service = new QobuzApiServiceFactory(
                    settings.AppId,
                    settings.AppSecret).Create();
                return service.LoginWithToken(settings.UserId, settings.UserAuthToken);
            }).ConfigureAwait(true);

            settings.UserAuthToken = login.AuthToken;
            await settingsStore.SaveAsync(settings).ConfigureAwait(true);
            Account.UpdateFromLogin(login, settings);
            Settings.UpdateAccountInfo(login);
        }
        catch (ApiErrorResponseException ex)
        {
            LogService.Warning("Account", SafeErrorText.FormatApiFailure("Account refresh", ex));
        }
        catch (Exception ex)
        {
            LogService.Warning("Account", SafeErrorText.FormatUnexpectedLogMessage(ex));
        }
    }

    public Task PrepareForShutdownAsync()
    {
        return Downloads.PrepareForShutdownAsync();
    }

    partial void OnSelectedPageChanged(AppPage value)
    {
        OnPropertyChanged(nameof(CurrentPage));
        OnPropertyChanged(nameof(IsDownloadsSelected));
        OnPropertyChanged(nameof(IsSearchSelected));
        OnPropertyChanged(nameof(IsTagsSelected));
        OnPropertyChanged(nameof(IsSettingsSelected));
        OnPropertyChanged(nameof(IsLogsSelected));
        OnPropertyChanged(nameof(IsLoginSelected));
        OnPropertyChanged(nameof(IsShellVisible));
        OnPropertyChanged(nameof(FooterLabel));
        OnPropertyChanged(nameof(FooterStatus));
    }
}
