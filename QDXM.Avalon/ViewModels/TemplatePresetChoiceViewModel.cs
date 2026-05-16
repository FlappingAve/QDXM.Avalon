using QDXM.Avalon.Core.Settings;

namespace QDXM.Avalon.ViewModels;

public sealed class TemplatePresetChoiceViewModel
{
    public TemplatePresetChoiceViewModel(TemplatePreset preset, bool isBuiltIn)
    {
        Preset = preset;
        IsBuiltIn = isBuiltIn;
    }

    public TemplatePreset Preset { get; }

    public bool IsBuiltIn { get; }

    public bool IsUserPreset => !IsBuiltIn;

    public string Id => Preset.Id;

    public string Name => Preset.Name;

    public string Template => Preset.Template;

    public string DisplayText => $"{Name} | {Template}";

    public override string ToString() => DisplayText;
}
