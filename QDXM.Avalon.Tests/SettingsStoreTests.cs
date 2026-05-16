using System.Reflection;
using QDXM.Avalon.Core.Settings;
using QDXM.Avalon.Core.Tools;

namespace QDXM.Avalon.Tests;

public sealed class SettingsStoreTests
{
    [Fact]
    public void AppSettings_DefaultsMatchFreshInstallSettings()
    {
        var settings = new AppSettings();

        Assert.True(settings.DownloadGoodies);
        Assert.True(settings.SaveCoverArtFile);
        Assert.Equal(QualityStringMappings.FlacHighestFormatId, settings.FormatId);
        Assert.Equal(QualityStringMappings.FlacHighestLabel, settings.SelectedQuality);
        Assert.False(settings.FallbackToMp3IfFlacUnavailable);
        Assert.Equal(AppSettings.DuplicateFileOverwrite, settings.DuplicateFileBehavior);
        Assert.Equal(CoverArtUrlSelector.RecommendedDisplayName, settings.CoverArtSize);
        Assert.Equal(AppSettings.DefaultFolderTemplate, settings.FolderTemplate);
        Assert.Equal(AppSettings.DefaultFilenameTemplate, settings.FilenameTemplate);
        Assert.Equal(AppSettings.DefaultDiscFolderTemplate, settings.DiscFolderTemplate);
        Assert.Equal("folder.default", settings.FolderTemplatePresetId);
        Assert.Equal("filename.default", settings.FilenameTemplatePresetId);
        Assert.Equal("discFolder.default", settings.DiscFolderTemplatePresetId);
        Assert.Equal(AppSettings.DefaultPlaylistOrganization, settings.PlaylistOrganization);
        Assert.Equal(AppSettings.DefaultPlaylistFolderTemplate, settings.PlaylistFolderTemplate);
        Assert.Equal(AppSettings.DefaultPlaylistFilenameTemplate, settings.PlaylistFilenameTemplate);
        Assert.Equal("playlistFolder.default", settings.PlaylistFolderTemplatePresetId);
        Assert.Equal("playlistFilename.default", settings.PlaylistFilenameTemplatePresetId);
        Assert.Empty(settings.TemplatePresets.Folder);
        Assert.Empty(settings.TemplatePresets.Filename);
        Assert.Empty(settings.TemplatePresets.DiscFolder);
        Assert.Empty(settings.TemplatePresets.PlaylistFolder);
        Assert.Empty(settings.TemplatePresets.PlaylistFilename);
        Assert.Equal(AppSettings.DefaultDiscWorkHandling, settings.DiscWorkHandling);
        Assert.Equal(AppSettings.DefaultDiscWorkSeparator, settings.DiscWorkSeparator);
        Assert.False(settings.DiscWorkSeparatorNoSpaces);
        Assert.True(settings.Tagging.WriteAlbumArtistTag);
        Assert.True(settings.Tagging.WriteAlbumNameTag);
        Assert.True(settings.Tagging.WriteTrackArtistTag);
        Assert.True(settings.Tagging.WriteTrackTitleTag);
        Assert.True(settings.Tagging.WriteReleaseYearTag);
        Assert.True(settings.Tagging.WriteReleaseTypeTag);
        Assert.True(settings.Tagging.WriteReleaseDateTag);
        Assert.True(settings.Tagging.WriteVersionTag);
        Assert.True(settings.Tagging.WriteWorkTag);
        Assert.True(settings.Tagging.WriteGenreTag);
        Assert.True(settings.Tagging.WriteTrackNumberTag);
        Assert.True(settings.Tagging.WriteTrackTotalTag);
        Assert.True(settings.Tagging.WriteDiscNumberTag);
        Assert.True(settings.Tagging.WriteDiscTotalTag);
        Assert.True(settings.Tagging.WriteComposerTag);
        Assert.True(settings.Tagging.WriteLabelTag);
        Assert.False(settings.Tagging.WriteRawQobuzCreditsTag);
        Assert.True(settings.Tagging.WriteCopyrightTag);
        Assert.True(settings.Tagging.WriteUpcTag);
        Assert.True(settings.Tagging.WriteIsrcTag);
        Assert.True(settings.Tagging.WriteCoverImageTag);
        Assert.True(settings.Tagging.WriteExplicitTag);
        Assert.False(settings.Tagging.WriteUrlTag);
        Assert.False(settings.Tagging.WriteCommentTag);
        Assert.Equal(string.Empty, settings.Tagging.CommentTag);
    }

    [Fact]
    public void AppSettings_EffectiveDownloadFolderFallsBackToMusicFolder()
    {
        var settings = new AppSettings { DownloadFolder = "  " };

        Assert.Equal(
            Environment.GetFolderPath(Environment.SpecialFolder.MyMusic),
            settings.EffectiveDownloadFolder);
    }

    [Fact]
    public void AppSettings_EffectiveDownloadFolderTrimsConfiguredFolder()
    {
        var settings = new AppSettings { DownloadFolder = @"  D:\Sort  " };

        Assert.Equal(@"D:\Sort", settings.EffectiveDownloadFolder);
    }

    [Fact]
    public void AppSettings_CreateSnapshotCopiesValuesAndTaggingOptions()
    {
        var settings = new AppSettings
        {
            DownloadFolder = @"D:\Before",
            FormatId = "27",
            FallbackToMp3IfFlacUnavailable = true,
            DuplicateFileBehavior = AppSettings.DuplicateFileKeepBoth,
            FolderTemplate = "Before Folder",
            FilenameTemplate = "Before Filename",
            DiscFolderTemplate = "Before Disc",
            FolderTemplatePresetId = "user.folder",
            FilenameTemplatePresetId = "user.filename",
            DiscFolderTemplatePresetId = "user.disc",
            PlaylistOrganization = AppSettings.UseStandardTemplatesPlaylistOrganization,
            PlaylistFolderTemplate = "Before Playlist Folder",
            PlaylistFilenameTemplate = "Before Playlist Filename",
            PlaylistFolderTemplatePresetId = "user.playlistFolder",
            PlaylistFilenameTemplatePresetId = "user.playlistFilename",
            TemplatePresets = new TemplatePresetSettings
            {
                Folder = [new TemplatePreset { Id = "user.folder", Name = "Before Folder Preset", Template = "Before Folder" }],
                Filename = [new TemplatePreset { Id = "user.filename", Name = "Before Filename Preset", Template = "Before Filename" }]
            },
            DiscWorkHandling = "Inline",
            DiscWorkSeparator = "&",
            DiscWorkSeparatorNoSpaces = false,
            MaxFileNameLength = 90,
            SaveCoverArtFile = true,
            CoverArtSize = "600 px",
            DownloadGoodies = true,
            Tagging = new TaggingOptions
            {
                WriteAlbumNameTag = true,
                WriteTrackTitleTag = true,
                WriteRawQobuzCreditsTag = false,
                CommentTag = "Before comment",
                ArtSize = "300 px"
            }
        };

        var snapshot = settings.CreateSnapshot();

        settings.DownloadFolder = @"D:\After";
        settings.FormatId = "6";
        settings.FallbackToMp3IfFlacUnavailable = false;
        settings.DuplicateFileBehavior = AppSettings.DuplicateFileSkip;
        settings.FolderTemplate = "After Folder";
        settings.FilenameTemplate = "After Filename";
        settings.DiscFolderTemplate = "After Disc";
        settings.FolderTemplatePresetId = "after.folder";
        settings.FilenameTemplatePresetId = "after.filename";
        settings.DiscFolderTemplatePresetId = "after.disc";
        settings.PlaylistOrganization = AppSettings.DefaultPlaylistOrganization;
        settings.PlaylistFolderTemplate = "After Playlist Folder";
        settings.PlaylistFilenameTemplate = "After Playlist Filename";
        settings.PlaylistFolderTemplatePresetId = "after.playlistFolder";
        settings.PlaylistFilenameTemplatePresetId = "after.playlistFilename";
        settings.TemplatePresets.Folder[0].Name = "After Folder Preset";
        settings.DiscWorkHandling = "Folders";
        settings.DiscWorkSeparator = "-";
        settings.DiscWorkSeparatorNoSpaces = true;
        settings.MaxFileNameLength = 120;
        settings.SaveCoverArtFile = false;
        settings.CoverArtSize = "Original (Big Size!)";
        settings.DownloadGoodies = false;
        settings.Tagging.WriteAlbumNameTag = false;
        settings.Tagging.WriteTrackTitleTag = false;
        settings.Tagging.WriteRawQobuzCreditsTag = true;
        settings.Tagging.CommentTag = "After comment";
        settings.Tagging.ArtSize = "Original (Big Size!)";

        Assert.Equal(@"D:\Before", snapshot.DownloadFolder);
        Assert.Equal("27", snapshot.FormatId);
        Assert.True(snapshot.FallbackToMp3IfFlacUnavailable);
        Assert.Equal(AppSettings.DuplicateFileKeepBoth, snapshot.DuplicateFileBehavior);
        Assert.Equal("Before Folder", snapshot.FolderTemplate);
        Assert.Equal("Before Filename", snapshot.FilenameTemplate);
        Assert.Equal("Before Disc", snapshot.DiscFolderTemplate);
        Assert.Equal("user.folder", snapshot.FolderTemplatePresetId);
        Assert.Equal("user.filename", snapshot.FilenameTemplatePresetId);
        Assert.Equal("user.disc", snapshot.DiscFolderTemplatePresetId);
        Assert.Equal(AppSettings.UseStandardTemplatesPlaylistOrganization, snapshot.PlaylistOrganization);
        Assert.Equal("Before Playlist Folder", snapshot.PlaylistFolderTemplate);
        Assert.Equal("Before Playlist Filename", snapshot.PlaylistFilenameTemplate);
        Assert.Equal("user.playlistFolder", snapshot.PlaylistFolderTemplatePresetId);
        Assert.Equal("user.playlistFilename", snapshot.PlaylistFilenameTemplatePresetId);
        Assert.NotSame(settings.TemplatePresets, snapshot.TemplatePresets);
        Assert.Equal("Before Folder Preset", snapshot.TemplatePresets.Folder[0].Name);
        Assert.Equal("Inline", snapshot.DiscWorkHandling);
        Assert.Equal("&", snapshot.DiscWorkSeparator);
        Assert.False(snapshot.DiscWorkSeparatorNoSpaces);
        Assert.Equal(90, snapshot.MaxFileNameLength);
        Assert.True(snapshot.SaveCoverArtFile);
        Assert.Equal("600 px", snapshot.CoverArtSize);
        Assert.True(snapshot.DownloadGoodies);
        Assert.NotSame(settings.Tagging, snapshot.Tagging);
        Assert.True(snapshot.Tagging.WriteAlbumNameTag);
        Assert.True(snapshot.Tagging.WriteTrackTitleTag);
        Assert.False(snapshot.Tagging.WriteRawQobuzCreditsTag);
        Assert.Equal("Before comment", snapshot.Tagging.CommentTag);
        Assert.Equal("300 px", snapshot.Tagging.ArtSize);
    }

    [Fact]
    public void AppSettings_CreateSnapshot_CopiesAllPublicSettableProperties()
    {
        var settings = new AppSettings();
        FillWithDummyData(settings);

        var snapshot = settings.CreateSnapshot();

        foreach (var property in GetSnapshotProperties<AppSettings>())
        {
            if (property.PropertyType == typeof(TaggingOptions) ||
                property.PropertyType == typeof(TemplatePresetSettings))
            {
                continue;
            }

            AssertPropertyWasCopied(settings, snapshot, property, nameof(AppSettings.CreateSnapshot));
        }
    }

    [Fact]
    public void TaggingOptions_CreateSnapshot_CopiesAllPublicSettableProperties()
    {
        var tagging = new TaggingOptions();
        FillWithDummyData(tagging);

        var snapshot = tagging.CreateSnapshot();

        foreach (var property in GetSnapshotProperties<TaggingOptions>())
        {
            AssertPropertyWasCopied(tagging, snapshot, property, nameof(TaggingOptions.CreateSnapshot));
        }
    }

    private static void FillWithDummyData(object obj)
    {
        var type = obj.GetType();
        var defaultObj = Activator.CreateInstance(type);
        foreach (var property in GetSnapshotProperties(type))
        {
            if (property.PropertyType == typeof(string))
            {
                property.SetValue(obj, "dummy_" + property.Name);
            }
            else if (property.PropertyType == typeof(bool))
            {
                property.SetValue(obj, !(bool)property.GetValue(defaultObj)!);
            }
            else if (property.PropertyType == typeof(int))
            {
                property.SetValue(obj, (int)property.GetValue(defaultObj)! + 1);
            }
            else if (property.PropertyType == typeof(TaggingOptions))
            {
                var tagging = new TaggingOptions();
                FillWithDummyData(tagging);
                property.SetValue(obj, tagging);
            }
            else if (property.PropertyType == typeof(TemplatePresetSettings))
            {
                property.SetValue(obj, new TemplatePresetSettings
                {
                    Folder = [new TemplatePreset { Id = "dummy_folder", Name = "Dummy Folder", Template = "Dummy Folder Template" }],
                    Filename = [new TemplatePreset { Id = "dummy_filename", Name = "Dummy Filename", Template = "Dummy Filename Template" }]
                });
            }
            else
            {
                throw new NotSupportedException(
                    $"Snapshot test dummy data does not support property {type.Name}.{property.Name} of type {property.PropertyType.Name}.");
            }
        }
    }

    private static IReadOnlyList<PropertyInfo> GetSnapshotProperties<T>()
    {
        return GetSnapshotProperties(typeof(T));
    }

    private static IReadOnlyList<PropertyInfo> GetSnapshotProperties(Type type)
    {
        return type
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(property => property.CanWrite)
            .ToList();
    }

    private static void AssertPropertyWasCopied<T>(T source, T snapshot, PropertyInfo property, string snapshotMethodName)
    {
        var originalValue = property.GetValue(source);
        var snapshotValue = property.GetValue(snapshot);

        Assert.True(
            Equals(originalValue, snapshotValue),
            $"Property {property.Name} was not copied by {typeof(T).Name}.{snapshotMethodName}(). Expected '{originalValue}', got '{snapshotValue}'.");
    }

    [Fact]
    public async Task JsonSettingsStore_RoundTripsSettings()
    {
        using var workspace = TestPaths.CreateWorkspace();
        var settingsPath = workspace.FilePath("settings.json");
        var store = new JsonSettingsStore(settingsPath, new InMemoryCredentialStore());
        var settings = new AppSettings
        {
            DownloadFolder = @"D:\Sort",
            SelectedQuality = QualityStringMappings.FlacHighestLabel,
            FallbackToMp3IfFlacUnavailable = true,
            DuplicateFileBehavior = AppSettings.DuplicateFileKeepBoth,
            FolderTemplate = "Artist-Album-Quality",
            FilenameTemplate = "00 - Trackname",
            PlaylistOrganization = AppSettings.UseStandardTemplatesPlaylistOrganization,
            PlaylistFolderTemplate = @"Mixes\{PlaylistTitle}",
            PlaylistFilenameTemplate = "{PlaylistNumberPadded} {TrackTitle}",
            DownloadGoodies = true
        };

        await store.SaveAsync(settings);
        var loaded = await store.LoadAsync();

        Assert.Equal(@"D:\Sort", loaded.DownloadFolder);
        Assert.Equal(QualityStringMappings.FlacHighestLabel, loaded.SelectedQuality);
        Assert.Equal(QualityStringMappings.FlacHighestFormatId, loaded.FormatId);
        Assert.True(loaded.FallbackToMp3IfFlacUnavailable);
        Assert.Equal(AppSettings.DuplicateFileKeepBoth, loaded.DuplicateFileBehavior);
        Assert.Equal(AppSettings.UseStandardTemplatesPlaylistOrganization, loaded.PlaylistOrganization);
        Assert.Equal(AppSettings.DefaultPlaylistFolderTemplate, loaded.PlaylistFolderTemplate);
        Assert.Equal(AppSettings.DefaultPlaylistFilenameTemplate, loaded.PlaylistFilenameTemplate);
        Assert.True(loaded.DownloadGoodies);
    }

    [Fact]
    public async Task JsonSettingsStore_RoundTripsTemplatePresets()
    {
        using var workspace = TestPaths.CreateWorkspace();
        var settingsPath = workspace.FilePath("settings.json");
        var store = new JsonSettingsStore(settingsPath, new InMemoryCredentialStore());
        var settings = new AppSettings
        {
            FolderTemplatePresetId = "user.folder",
            FilenameTemplatePresetId = "user.filename",
            TemplatePresets = new TemplatePresetSettings
            {
                Folder =
                [
                    new TemplatePreset
                    {
                        Id = "user.folder",
                        Name = "Lossless Archive",
                        Template = @"{AlbumArtist}\{ReleaseYear} - {AlbumTitle}"
                    }
                ],
                Filename =
                [
                    new TemplatePreset
                    {
                        Id = "user.filename",
                        Name = "Track Title",
                        Template = "{TrackNumberPadded}. {TrackTitle}"
                    }
                ]
            }
        };

        await store.SaveAsync(settings);
        var loaded = await store.LoadAsync();

        Assert.Equal("user.folder", loaded.FolderTemplatePresetId);
        Assert.Equal(@"{AlbumArtist}\{ReleaseYear} - {AlbumTitle}", loaded.FolderTemplate);
        Assert.Equal("Lossless Archive", loaded.TemplatePresets.Folder.Single().Name);
        Assert.Equal("user.filename", loaded.FilenameTemplatePresetId);
        Assert.Equal("{TrackNumberPadded}. {TrackTitle}", loaded.FilenameTemplate);
        Assert.Equal("Track Title", loaded.TemplatePresets.Filename.Single().Name);
    }

    [Fact]
    public async Task JsonSettingsStore_SaveTemplatePresetSlotOnlyPersistsThatSlot()
    {
        using var workspace = TestPaths.CreateWorkspace();
        var settingsPath = workspace.FilePath("settings.json");
        var store = new JsonSettingsStore(settingsPath, new InMemoryCredentialStore());
        await store.SaveAsync(new AppSettings
        {
            DownloadFolder = @"D:\Stored",
            DiscWorkSeparator = "&"
        });
        await store.LoadAsync();

        store.Current.DownloadFolder = @"D:\Unsaved";
        store.Current.DiscWorkSeparator = "+";
        await store.SaveTemplatePresetSlotAsync(
            TemplatePresetSlots.Folder,
            "user.folder",
            [
                new TemplatePreset
                {
                    Id = "user.folder",
                    Name = "Folder Test",
                    Template = @"{AlbumArtist}\{AlbumTitle}"
                }
            ]);

        var reloaded = await new JsonSettingsStore(settingsPath, new InMemoryCredentialStore()).LoadAsync();

        Assert.Equal(@"D:\Stored", reloaded.DownloadFolder);
        Assert.Equal("&", reloaded.DiscWorkSeparator);
        Assert.Equal("user.folder", reloaded.FolderTemplatePresetId);
        Assert.Equal("Folder Test", reloaded.TemplatePresets.Folder.Single().Name);
        Assert.Equal(@"D:\Unsaved", store.Current.DownloadFolder);
        Assert.Equal("+", store.Current.DiscWorkSeparator);
        Assert.Equal("user.folder", store.Current.FolderTemplatePresetId);
    }

    [Fact]
    public async Task JsonSettingsStore_DefaultsMissingOrNullPlaylistSettings()
    {
        using var workspace = TestPaths.CreateWorkspace();
        var settingsPath = workspace.FilePath("settings.json");
        await File.WriteAllTextAsync(settingsPath, """
            {
              "playlistOrganization": null,
              "playlistFolderTemplate": null,
              "playlistFilenameTemplate": ""
            }
            """);

        var store = new JsonSettingsStore(settingsPath, new InMemoryCredentialStore());
        var loaded = await store.LoadAsync();

        Assert.Equal(AppSettings.DefaultPlaylistOrganization, loaded.PlaylistOrganization);
        Assert.Equal(AppSettings.DefaultPlaylistFolderTemplate, loaded.PlaylistFolderTemplate);
        Assert.Equal(AppSettings.DefaultPlaylistFilenameTemplate, loaded.PlaylistFilenameTemplate);
    }

    [Fact]
    public async Task JsonSettingsStore_DefaultsUnsavedBlankDiscFolderTemplate()
    {
        using var workspace = TestPaths.CreateWorkspace();
        var settingsPath = workspace.FilePath("settings.json");
        await File.WriteAllTextAsync(settingsPath, """
            {
              "discFolderTemplate": ""
            }
            """);

        var store = new JsonSettingsStore(settingsPath, new InMemoryCredentialStore());
        var loaded = await store.LoadAsync();

        Assert.Equal(AppSettings.DefaultDiscFolderTemplate, loaded.DiscFolderTemplate);
    }

    [Fact]
    public async Task JsonSettingsStore_DefaultsUnsavedBlankFolderTemplate()
    {
        using var workspace = TestPaths.CreateWorkspace();
        var settingsPath = workspace.FilePath("settings.json");
        await File.WriteAllTextAsync(settingsPath, """
            {
              "folderTemplate": ""
            }
            """);

        var store = new JsonSettingsStore(settingsPath, new InMemoryCredentialStore());
        var loaded = await store.LoadAsync();

        Assert.Equal(AppSettings.DefaultFolderTemplate, loaded.FolderTemplate);
    }

    [Fact]
    public async Task JsonSettingsStore_StoresCredentialsOutsideJson()
    {
        using var workspace = TestPaths.CreateWorkspace();
        var settingsPath = workspace.FilePath("settings.json");
        var credentialStore = new InMemoryCredentialStore();
        var store = new JsonSettingsStore(settingsPath, credentialStore);

        await store.SaveAsync(new AppSettings
        {
            AppId = "manual-app-id",
            AppSecret = "manual-app-secret",
            UserId = "secret-user-id",
            UserAuthToken = "secret-token"
        });

        var json = await File.ReadAllTextAsync(settingsPath);
        Assert.DoesNotContain("manual-app-id", json);
        Assert.DoesNotContain("manual-app-secret", json);
        Assert.DoesNotContain("secret-user-id", json);
        Assert.DoesNotContain("secret-token", json);

        var reloaded = await new JsonSettingsStore(settingsPath, credentialStore).LoadAsync();
        Assert.Equal("manual-app-id", reloaded.AppId);
        Assert.Equal("manual-app-secret", reloaded.AppSecret);
        Assert.Equal("secret-user-id", reloaded.UserId);
        Assert.Equal("secret-token", reloaded.UserAuthToken);
    }

    private sealed class InMemoryCredentialStore : IUserCredentialStore
    {
        private UserCredential? credential;

        public Task<UserCredential?> ReadAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(credential);
        }

        public Task SaveAsync(UserCredential credential, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            this.credential = credential;
            return Task.CompletedTask;
        }

        public Task DeleteAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            credential = null;
            return Task.CompletedTask;
        }
    }
}
