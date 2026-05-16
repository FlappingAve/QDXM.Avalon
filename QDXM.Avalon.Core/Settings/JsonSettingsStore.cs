using System.Text.Json;
using QDXM.Avalon.Core.Tools;

namespace QDXM.Avalon.Core.Settings;

public sealed class JsonSettingsStore : ISettingsStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly string settingsPath;
    private readonly IUserCredentialStore credentialStore;

    public JsonSettingsStore(
        string? settingsPath = null,
        IUserCredentialStore? credentialStore = null)
    {
        this.settingsPath = settingsPath ?? AppDataPaths.SettingsFilePath;
        this.credentialStore = credentialStore ?? new WindowsCredentialStore();
    }

    public AppSettings Current { get; private set; } = new();

    public async Task<AppSettings> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(settingsPath))
        {
            Current = new AppSettings();
            ApplyCredential(await credentialStore.ReadAsync(cancellationToken).ConfigureAwait(false));
            return Current;
        }

        Current = JsonSerializer.Deserialize<AppSettings>(
            await File.ReadAllTextAsync(settingsPath, cancellationToken).ConfigureAwait(false),
            SerializerOptions) ?? new AppSettings();
        ApplySettingsMigrations(Current);

        var storedCredential = await credentialStore.ReadAsync(cancellationToken).ConfigureAwait(false);
        if (storedCredential is not null)
        {
            ApplyCredential(storedCredential);
        }

        return Current;
    }

    public async Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default)
    {
        ApplySettingsMigrations(settings);

        var credential = new UserCredential(
            settings.UserId,
            settings.UserAuthToken,
            settings.AppId,
            settings.AppSecret);
        if (string.IsNullOrWhiteSpace(credential.UserId) &&
            string.IsNullOrWhiteSpace(credential.UserAuthToken) &&
            string.IsNullOrWhiteSpace(credential.AppId) &&
            string.IsNullOrWhiteSpace(credential.AppSecret))
        {
            await credentialStore.DeleteAsync(cancellationToken).ConfigureAwait(false);
        }
        else
        {
            await credentialStore.SaveAsync(credential, cancellationToken).ConfigureAwait(false);
        }

        await WriteSettingsJsonAsync(settings, cancellationToken).ConfigureAwait(false);
        Current = settings;
    }

    public async Task SaveTemplatePresetSlotAsync(
        string slot,
        string selectedPresetId,
        IReadOnlyList<TemplatePreset> userPresets,
        CancellationToken cancellationToken = default)
    {
        var stored = await ReadStoredSettingsAsync(cancellationToken).ConfigureAwait(false);
        ApplyTemplatePresetSlot(stored, slot, selectedPresetId, userPresets);
        await WriteSettingsJsonAsync(stored, cancellationToken).ConfigureAwait(false);

        ApplyTemplatePresetSlot(Current, slot, selectedPresetId, userPresets);
    }

    private void ApplyCredential(UserCredential? credential)
    {
        if (credential is null)
        {
            return;
        }

        Current.UserId = credential.UserId;
        Current.UserAuthToken = credential.UserAuthToken;
        Current.AppId = credential.AppId;
        Current.AppSecret = credential.AppSecret;
    }

    private static void ApplySettingsMigrations(AppSettings settings)
    {
        var normalizedFormatId = QualityStringMappings.GetFormatIdFromQualityLabel(settings.SelectedQuality);
        if (string.IsNullOrWhiteSpace(normalizedFormatId))
        {
            normalizedFormatId = settings.FormatId == QualityStringMappings.Mp3FormatId
                ? QualityStringMappings.Mp3FormatId
                : QualityStringMappings.FlacHighestFormatId;
        }

        settings.FormatId = normalizedFormatId;
        settings.SelectedQuality = QualityStringMappings.GetQualityLabelFromFormatId(normalizedFormatId);

        if (settings.DuplicateFileBehavior is not AppSettings.DuplicateFileSkip &&
            settings.DuplicateFileBehavior is not AppSettings.DuplicateFileOverwrite &&
            settings.DuplicateFileBehavior is not AppSettings.DuplicateFileKeepBoth)
        {
            settings.DuplicateFileBehavior = AppSettings.DuplicateFileOverwrite;
        }

        if (string.IsNullOrWhiteSpace(settings.DiscWorkHandling))
        {
            settings.DiscWorkHandling = AppSettings.DefaultDiscWorkHandling;
        }

        if (string.IsNullOrWhiteSpace(settings.DiscWorkSeparator))
        {
            settings.DiscWorkSeparator = AppSettings.DefaultDiscWorkSeparator;
        }

        if (settings.PlaylistOrganization is not AppSettings.DefaultPlaylistOrganization &&
            settings.PlaylistOrganization is not AppSettings.UseStandardTemplatesPlaylistOrganization)
        {
            settings.PlaylistOrganization = AppSettings.DefaultPlaylistOrganization;
        }

        if (string.IsNullOrWhiteSpace(settings.PlaylistFolderTemplate))
        {
            settings.PlaylistFolderTemplate = AppSettings.DefaultPlaylistFolderTemplate;
        }

        if (string.IsNullOrWhiteSpace(settings.PlaylistFilenameTemplate))
        {
            settings.PlaylistFilenameTemplate = AppSettings.DefaultPlaylistFilenameTemplate;
        }

        settings.TemplatePresets ??= new TemplatePresetSettings();
        foreach (var slot in TemplatePresetCatalog.AllSlots)
        {
            TemplatePresetCatalog.NormalizeUserPresets(settings, slot);
            TemplatePresetCatalog.ApplyResolvedPreset(settings, slot);
        }
    }

    private async Task<AppSettings> ReadStoredSettingsAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(settingsPath))
        {
            return new AppSettings();
        }

        var settings = JsonSerializer.Deserialize<AppSettings>(
            await File.ReadAllTextAsync(settingsPath, cancellationToken).ConfigureAwait(false),
            SerializerOptions) ?? new AppSettings();
        ApplySettingsMigrations(settings);
        return settings;
    }

    private static void ApplyTemplatePresetSlot(
        AppSettings settings,
        string slot,
        string selectedPresetId,
        IReadOnlyList<TemplatePreset> userPresets)
    {
        TemplatePresetCatalog.SetUserPresets(settings, slot, userPresets);
        TemplatePresetCatalog.SetSelectedPresetId(settings, slot, selectedPresetId);
        TemplatePresetCatalog.NormalizeUserPresets(settings, slot);
        TemplatePresetCatalog.ApplyResolvedPreset(settings, slot);
    }

    private Task WriteSettingsJsonAsync(AppSettings settings, CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(settingsPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var json = JsonSerializer.Serialize(settings, SerializerOptions);
        return File.WriteAllTextAsync(settingsPath, json, cancellationToken);
    }

}
