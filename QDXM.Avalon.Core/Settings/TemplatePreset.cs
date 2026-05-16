namespace QDXM.Avalon.Core.Settings;

public sealed class TemplatePreset
{
    public string Id { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string Template { get; set; } = string.Empty;

    public TemplatePreset CreateSnapshot()
    {
        return new TemplatePreset
        {
            Id = Id,
            Name = Name,
            Template = Template
        };
    }
}

public sealed class TemplatePresetSettings
{
    public List<TemplatePreset> Folder { get; set; } = [];

    public List<TemplatePreset> Filename { get; set; } = [];

    public List<TemplatePreset> DiscFolder { get; set; } = [];

    public List<TemplatePreset> PlaylistFolder { get; set; } = [];

    public List<TemplatePreset> PlaylistFilename { get; set; } = [];

    public TemplatePresetSettings CreateSnapshot()
    {
        return new TemplatePresetSettings
        {
            Folder = Copy(Folder ?? []),
            Filename = Copy(Filename ?? []),
            DiscFolder = Copy(DiscFolder ?? []),
            PlaylistFolder = Copy(PlaylistFolder ?? []),
            PlaylistFilename = Copy(PlaylistFilename ?? [])
        };
    }

    private static List<TemplatePreset> Copy(IEnumerable<TemplatePreset> presets)
    {
        return presets.Select(preset => preset.CreateSnapshot()).ToList();
    }
}

public static class TemplatePresetSlots
{
    public const string Folder = "folder";
    public const string Filename = "filename";
    public const string DiscFolder = "discFolder";
    public const string PlaylistFolder = "playlistFolder";
    public const string PlaylistFilename = "playlistFilename";
}

public static class TemplatePresetCatalog
{
    private static readonly IReadOnlyList<TemplatePreset> FolderBuiltIns =
    [
        new() { Id = "folder.default", Name = "Default", Template = AppSettings.DefaultFolderTemplate }
    ];

    private static readonly IReadOnlyList<TemplatePreset> FilenameBuiltIns =
    [
        new() { Id = "filename.default", Name = "Default", Template = AppSettings.DefaultFilenameTemplate }
    ];

    private static readonly IReadOnlyList<TemplatePreset> DiscFolderBuiltIns =
    [
        new() { Id = "discFolder.default", Name = "Default", Template = AppSettings.DefaultDiscFolderTemplate }
    ];

    private static readonly IReadOnlyList<TemplatePreset> PlaylistFolderBuiltIns =
    [
        new() { Id = "playlistFolder.default", Name = "Default", Template = AppSettings.DefaultPlaylistFolderTemplate }
    ];

    private static readonly IReadOnlyList<TemplatePreset> PlaylistFilenameBuiltIns =
    [
        new() { Id = "playlistFilename.default", Name = "Default", Template = AppSettings.DefaultPlaylistFilenameTemplate }
    ];

    public static IReadOnlyList<TemplatePreset> GetBuiltInPresets(string slot)
    {
        return slot switch
        {
            TemplatePresetSlots.Folder => FolderBuiltIns,
            TemplatePresetSlots.Filename => FilenameBuiltIns,
            TemplatePresetSlots.DiscFolder => DiscFolderBuiltIns,
            TemplatePresetSlots.PlaylistFolder => PlaylistFolderBuiltIns,
            TemplatePresetSlots.PlaylistFilename => PlaylistFilenameBuiltIns,
            _ => throw new ArgumentOutOfRangeException(nameof(slot), slot, "Unknown template preset slot.")
        };
    }

    public static IReadOnlyList<string> AllSlots { get; } =
    [
        TemplatePresetSlots.Folder,
        TemplatePresetSlots.Filename,
        TemplatePresetSlots.DiscFolder,
        TemplatePresetSlots.PlaylistFolder,
        TemplatePresetSlots.PlaylistFilename
    ];

    public static List<TemplatePreset> GetUserPresets(AppSettings settings, string slot)
    {
        EnsurePresetSettings(settings);
        return slot switch
        {
            TemplatePresetSlots.Folder => settings.TemplatePresets.Folder,
            TemplatePresetSlots.Filename => settings.TemplatePresets.Filename,
            TemplatePresetSlots.DiscFolder => settings.TemplatePresets.DiscFolder,
            TemplatePresetSlots.PlaylistFolder => settings.TemplatePresets.PlaylistFolder,
            TemplatePresetSlots.PlaylistFilename => settings.TemplatePresets.PlaylistFilename,
            _ => throw new ArgumentOutOfRangeException(nameof(slot), slot, "Unknown template preset slot.")
        };
    }

    public static void SetUserPresets(AppSettings settings, string slot, IEnumerable<TemplatePreset> presets)
    {
        EnsurePresetSettings(settings);
        var copy = presets.Select(preset => preset.CreateSnapshot()).ToList();
        switch (slot)
        {
            case TemplatePresetSlots.Folder:
                settings.TemplatePresets.Folder = copy;
                break;
            case TemplatePresetSlots.Filename:
                settings.TemplatePresets.Filename = copy;
                break;
            case TemplatePresetSlots.DiscFolder:
                settings.TemplatePresets.DiscFolder = copy;
                break;
            case TemplatePresetSlots.PlaylistFolder:
                settings.TemplatePresets.PlaylistFolder = copy;
                break;
            case TemplatePresetSlots.PlaylistFilename:
                settings.TemplatePresets.PlaylistFilename = copy;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(slot), slot, "Unknown template preset slot.");
        }
    }

    public static string GetSelectedPresetId(AppSettings settings, string slot)
    {
        return slot switch
        {
            TemplatePresetSlots.Folder => settings.FolderTemplatePresetId,
            TemplatePresetSlots.Filename => settings.FilenameTemplatePresetId,
            TemplatePresetSlots.DiscFolder => settings.DiscFolderTemplatePresetId,
            TemplatePresetSlots.PlaylistFolder => settings.PlaylistFolderTemplatePresetId,
            TemplatePresetSlots.PlaylistFilename => settings.PlaylistFilenameTemplatePresetId,
            _ => throw new ArgumentOutOfRangeException(nameof(slot), slot, "Unknown template preset slot.")
        };
    }

    public static void SetSelectedPresetId(AppSettings settings, string slot, string presetId)
    {
        switch (slot)
        {
            case TemplatePresetSlots.Folder:
                settings.FolderTemplatePresetId = presetId;
                break;
            case TemplatePresetSlots.Filename:
                settings.FilenameTemplatePresetId = presetId;
                break;
            case TemplatePresetSlots.DiscFolder:
                settings.DiscFolderTemplatePresetId = presetId;
                break;
            case TemplatePresetSlots.PlaylistFolder:
                settings.PlaylistFolderTemplatePresetId = presetId;
                break;
            case TemplatePresetSlots.PlaylistFilename:
                settings.PlaylistFilenameTemplatePresetId = presetId;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(slot), slot, "Unknown template preset slot.");
        }
    }

    public static string GetTemplateValue(AppSettings settings, string slot)
    {
        return slot switch
        {
            TemplatePresetSlots.Folder => settings.FolderTemplate,
            TemplatePresetSlots.Filename => settings.FilenameTemplate,
            TemplatePresetSlots.DiscFolder => settings.DiscFolderTemplate,
            TemplatePresetSlots.PlaylistFolder => settings.PlaylistFolderTemplate,
            TemplatePresetSlots.PlaylistFilename => settings.PlaylistFilenameTemplate,
            _ => throw new ArgumentOutOfRangeException(nameof(slot), slot, "Unknown template preset slot.")
        };
    }

    public static void SetTemplateValue(AppSettings settings, string slot, string template)
    {
        switch (slot)
        {
            case TemplatePresetSlots.Folder:
                settings.FolderTemplate = template;
                break;
            case TemplatePresetSlots.Filename:
                settings.FilenameTemplate = template;
                break;
            case TemplatePresetSlots.DiscFolder:
                settings.DiscFolderTemplate = template;
                break;
            case TemplatePresetSlots.PlaylistFolder:
                settings.PlaylistFolderTemplate = template;
                break;
            case TemplatePresetSlots.PlaylistFilename:
                settings.PlaylistFilenameTemplate = template;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(slot), slot, "Unknown template preset slot.");
        }
    }

    public static TemplatePreset ResolveSelectedPreset(AppSettings settings, string slot)
    {
        var selectedId = GetSelectedPresetId(settings, slot);
        return FindPreset(settings, slot, selectedId) ??
               FindPresetByTemplate(settings, slot, GetTemplateValue(settings, slot)) ??
               GetBuiltInPresets(slot)[0];
    }

    private static TemplatePreset? FindPreset(AppSettings settings, string slot, string? presetId)
    {
        if (string.IsNullOrWhiteSpace(presetId))
        {
            return null;
        }

        return GetBuiltInPresets(slot)
            .Concat(GetUserPresets(settings, slot))
            .FirstOrDefault(preset => string.Equals(preset.Id, presetId, StringComparison.Ordinal));
    }

    private static TemplatePreset? FindPresetByTemplate(AppSettings settings, string slot, string? template)
    {
        return GetBuiltInPresets(slot)
            .Concat(GetUserPresets(settings, slot))
            .FirstOrDefault(preset => string.Equals(preset.Template, template ?? string.Empty, StringComparison.Ordinal));
    }

    public static void ApplyResolvedPreset(AppSettings settings, string slot)
    {
        var selectedPreset = ResolveSelectedPreset(settings, slot);
        SetSelectedPresetId(settings, slot, selectedPreset.Id);
        SetTemplateValue(settings, slot, selectedPreset.Template);
    }

    public static void NormalizeUserPresets(AppSettings settings, string slot)
    {
        EnsurePresetSettings(settings);
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var ids = new HashSet<string>(GetBuiltInPresets(slot).Select(preset => preset.Id), StringComparer.Ordinal);
        var normalized = new List<TemplatePreset>();

        foreach (var preset in GetUserPresets(settings, slot))
        {
            var name = preset.Name?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(name) || !names.Add(name))
            {
                continue;
            }

            var id = string.IsNullOrWhiteSpace(preset.Id)
                ? CreateUserPresetId()
                : preset.Id.Trim();
            if (!ids.Add(id))
            {
                id = CreateUserPresetId();
                ids.Add(id);
            }

            normalized.Add(new TemplatePreset
            {
                Id = id,
                Name = name,
                Template = preset.Template?.Trim() ?? string.Empty
            });
        }

        SetUserPresets(settings, slot, normalized);
    }

    public static string CreateUserPresetId()
    {
        return $"user.{Guid.NewGuid():N}";
    }

    private static void EnsurePresetSettings(AppSettings settings)
    {
        settings.TemplatePresets ??= new TemplatePresetSettings();
        settings.TemplatePresets.Folder ??= [];
        settings.TemplatePresets.Filename ??= [];
        settings.TemplatePresets.DiscFolder ??= [];
        settings.TemplatePresets.PlaylistFolder ??= [];
        settings.TemplatePresets.PlaylistFilename ??= [];
    }
}
