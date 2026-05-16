namespace QDXM.Avalon.Core.Settings;

public interface ISettingsStore
{
    AppSettings Current { get; }

    Task<AppSettings> LoadAsync(CancellationToken cancellationToken = default);

    Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default);

    Task SaveTemplatePresetSlotAsync(
        string slot,
        string selectedPresetId,
        IReadOnlyList<TemplatePreset> userPresets,
        CancellationToken cancellationToken = default);
}
