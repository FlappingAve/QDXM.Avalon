using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using QDXM.Avalon.Core.Settings;

namespace QDXM.Avalon.ViewModels;

public sealed partial class TemplatePresetSlotViewModel : ObservableObject
{
    private readonly Action<TemplatePresetSlotViewModel> activePresetChanged;
    private readonly Func<string, string?>? templateValidator;
    private bool refreshingSelection;
    private TemplatePresetChoiceViewModel activeChoice = null!;

    public TemplatePresetSlotViewModel(
        string slot,
        string displayName,
        bool allowBlankTemplate,
        Action<TemplatePresetSlotViewModel> activePresetChanged,
        Func<string, string?>? templateValidator = null)
    {
        Slot = slot;
        DisplayName = displayName;
        AllowBlankTemplate = allowBlankTemplate;
        this.activePresetChanged = activePresetChanged;
        this.templateValidator = templateValidator;
    }

    public string Slot { get; }

    public string DisplayName { get; }

    public bool AllowBlankTemplate { get; }

    public ObservableCollection<TemplatePresetChoiceViewModel> Options { get; } = [];

    [ObservableProperty]
    private TemplatePresetChoiceViewModel? selectedChoice;

    [ObservableProperty]
    private string draftName = string.Empty;

    [ObservableProperty]
    private string draftTemplate = string.Empty;

    [ObservableProperty]
    private string errorText = string.Empty;

    [ObservableProperty]
    private bool isEditorOpen;

    [ObservableProperty]
    private bool isNewTemplateDraft;

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorText);

    public string TemplateValidationErrorText => IsEditorOpen
        ? templateValidator?.Invoke(DraftTemplate.Trim()) ?? string.Empty
        : string.Empty;

    public bool HasTemplateValidationError => !string.IsNullOrWhiteSpace(TemplateValidationErrorText);

    public bool CanEditSelected => SelectedChoice?.IsUserPreset == true && !IsEditorOpen;

    public bool CanDeleteSelected => SelectedChoice?.IsUserPreset == true && !IsEditorOpen;

    public string EditorTitle => IsNewTemplateDraft
        ? $"New {DisplayName} Template"
        : $"Edit {DisplayName} Template";

    public string SelectedPresetId => activeChoice.Id;

    public string SelectedTemplate => activeChoice.Template;

    public IReadOnlyList<TemplatePreset> UserPresets => Options
        .Where(option => option.IsUserPreset)
        .Select(option => option.Preset.CreateSnapshot())
        .ToList();

    public void Refresh(AppSettings settings)
    {
        Options.Clear();

        foreach (var preset in TemplatePresetCatalog.GetBuiltInPresets(Slot))
        {
            Options.Add(new TemplatePresetChoiceViewModel(preset, isBuiltIn: true));
        }

        foreach (var preset in TemplatePresetCatalog.GetUserPresets(settings, Slot))
        {
            Options.Add(new TemplatePresetChoiceViewModel(preset, isBuiltIn: false));
        }

        var selectedPreset = TemplatePresetCatalog.ResolveSelectedPreset(settings, Slot);
        activeChoice = Options.First(option => option.Id == selectedPreset.Id);

        refreshingSelection = true;
        SelectedChoice = activeChoice;
        refreshingSelection = false;

        ResetDraft();
        ClearError();
        NotifyStateChanged();
    }

    public void BeginNew()
    {
        DraftName = string.Empty;
        DraftTemplate = activeChoice.Template;
        IsNewTemplateDraft = true;
        IsEditorOpen = true;
        ClearError();
        NotifyStateChanged();
    }

    public void BeginEdit()
    {
        if (SelectedChoice?.IsUserPreset != true)
        {
            return;
        }

        DraftName = SelectedChoice.Name;
        DraftTemplate = SelectedChoice.Template;
        IsNewTemplateDraft = false;
        IsEditorOpen = true;
        ClearError();
        NotifyStateChanged();
    }

    public void CancelEdit()
    {
        IsEditorOpen = false;
        IsNewTemplateDraft = false;
        ResetDraft();
        ClearError();
        NotifyStateChanged();
    }

    public bool CommitDraft()
    {
        if (!IsEditorOpen || SelectedChoice is null)
        {
            return true;
        }

        var name = DraftName.Trim();
        var template = DraftTemplate.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            SetError("Template name is required.");
            return false;
        }

        if (!AllowBlankTemplate && string.IsNullOrWhiteSpace(template))
        {
            SetError("Template is required.");
            return false;
        }

        var validationError = templateValidator?.Invoke(template);
        if (!string.IsNullOrWhiteSpace(validationError))
        {
            SetError(validationError);
            return false;
        }

        var editingId = IsNewTemplateDraft ? null : SelectedChoice.Id;
        if (HasDuplicateName(name, editingId))
        {
            SetError("Use a unique template name.");
            return false;
        }

        if (IsNewTemplateDraft)
        {
            AddUserPreset(name, template);
        }
        else
        {
            ReplaceUserPreset(SelectedChoice.Id, name, template);
        }

        IsEditorOpen = false;
        IsNewTemplateDraft = false;
        ResetDraft();
        ClearError();
        NotifyStateChanged();
        activePresetChanged(this);
        return true;
    }

    public bool DeleteSelected()
    {
        if (SelectedChoice?.IsUserPreset != true)
        {
            return false;
        }

        Options.Remove(SelectedChoice);
        IsEditorOpen = false;
        IsNewTemplateDraft = false;
        activeChoice = Options.First(option => option.IsBuiltIn);

        refreshingSelection = true;
        SelectedChoice = activeChoice;
        refreshingSelection = false;

        ResetDraft();
        ClearError();
        NotifyStateChanged();
        activePresetChanged(this);
        return true;
    }

    partial void OnSelectedChoiceChanged(TemplatePresetChoiceViewModel? value)
    {
        if (refreshingSelection || value is null)
        {
            return;
        }

        ClearError();
        activeChoice = value;
        IsEditorOpen = false;
        IsNewTemplateDraft = false;
        ResetDraft();
        NotifyStateChanged();
        activePresetChanged(this);
    }

    partial void OnErrorTextChanged(string value)
    {
        OnPropertyChanged(nameof(HasError));
    }

    partial void OnDraftTemplateChanged(string value)
    {
        NotifyTemplateValidationChanged();
    }

    partial void OnIsEditorOpenChanged(bool value)
    {
        NotifyStateChanged();
        NotifyTemplateValidationChanged();
    }

    partial void OnIsNewTemplateDraftChanged(bool value)
    {
        OnPropertyChanged(nameof(EditorTitle));
    }

    private void AddUserPreset(string name, string template)
    {
        var preset = new TemplatePreset
        {
            Id = TemplatePresetCatalog.CreateUserPresetId(),
            Name = name,
            Template = template
        };
        var choice = new TemplatePresetChoiceViewModel(preset, isBuiltIn: false);
        Options.Add(choice);
        SelectCommittedChoice(choice);
    }

    private void ReplaceUserPreset(string presetId, string name, string template)
    {
        var index = Options
            .Select((option, optionIndex) => (option, optionIndex))
            .First(pair => pair.option.Id == presetId)
            .optionIndex;

        var choice = new TemplatePresetChoiceViewModel(
            new TemplatePreset
            {
                Id = presetId,
                Name = name,
                Template = template
            },
            isBuiltIn: false);
        Options[index] = choice;
        SelectCommittedChoice(choice);
    }

    private void SelectCommittedChoice(TemplatePresetChoiceViewModel choice)
    {
        activeChoice = choice;
        refreshingSelection = true;
        SelectedChoice = choice;
        refreshingSelection = false;
    }

    private bool HasDuplicateName(string name, string? editingId)
    {
        return Options.Any(option =>
            !string.Equals(option.Id, editingId, StringComparison.Ordinal) &&
            string.Equals(option.Name, name, StringComparison.OrdinalIgnoreCase));
    }

    private void ResetDraft()
    {
        DraftName = string.Empty;
        DraftTemplate = string.Empty;
    }

    private void ClearError()
    {
        ErrorText = string.Empty;
    }

    private void SetError(string message)
    {
        ErrorText = message;
    }

    private void NotifyStateChanged()
    {
        OnPropertyChanged(nameof(CanEditSelected));
        OnPropertyChanged(nameof(CanDeleteSelected));
        OnPropertyChanged(nameof(EditorTitle));
        OnPropertyChanged(nameof(SelectedPresetId));
        OnPropertyChanged(nameof(SelectedTemplate));
    }

    private void NotifyTemplateValidationChanged()
    {
        OnPropertyChanged(nameof(TemplateValidationErrorText));
        OnPropertyChanged(nameof(HasTemplateValidationError));
    }
}
