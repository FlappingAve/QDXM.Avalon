using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace QDXM.Avalon.ViewModels;

public partial class AlbumTrackSelectionViewModel : ViewModelBase
{
    private readonly Action<AlbumTrackSelectionViewModel>? selectionChanged;
    private readonly Func<AlbumTrackSelectionViewModel, Task>? previewRequested;

    public AlbumTrackSelectionViewModel(
        string trackId,
        int trackNumber,
        int discNumber,
        string title,
        string version,
        string work,
        string composer,
        string duration,
        string quality,
        bool isSelected,
        Action<AlbumTrackSelectionViewModel>? selectionChanged = null,
        string? selectionKey = null,
        string? playlistPositionDisplay = null,
        int? albumTrackNumber = null,
        int? albumDiscNumber = null,
        string? albumPositionDisplay = null,
        string? artist = null,
        string? albumTitle = null,
        Func<AlbumTrackSelectionViewModel, Task>? previewRequested = null)
    {
        TrackId = trackId;
        TrackNumber = trackNumber;
        DiscNumber = discNumber;
        Title = title;
        Version = version;
        Work = work;
        Composer = composer;
        Duration = duration;
        Quality = quality;
        SelectionKey = selectionKey ?? trackId;
        PlaylistPositionDisplay = playlistPositionDisplay ?? TrackNumberDisplay;
        AlbumTrackNumber = albumTrackNumber ?? trackNumber;
        AlbumDiscNumber = albumDiscNumber ?? discNumber;
        AlbumPositionDisplay = albumPositionDisplay ?? string.Empty;
        Artist = artist ?? string.Empty;
        AlbumTitle = albumTitle ?? string.Empty;
        this.selectionChanged = selectionChanged;
        this.previewRequested = previewRequested;
        this.isSelected = isSelected;
    }

    public string TrackId { get; }
    public int TrackNumber { get; }
    public int DiscNumber { get; }
    public string TrackNumberDisplay => TrackNumber.ToString("00");
    public string SelectionKey { get; }
    public string PlaylistPositionDisplay { get; }
    public int AlbumTrackNumber { get; }
    public int AlbumDiscNumber { get; }
    public string AlbumPositionDisplay { get; }
    public string Title { get; }
    public string Version { get; }
    public bool HasVersion => !string.IsNullOrWhiteSpace(Version);
    public string Artist { get; }
    public string AlbumTitle { get; }
    public string Work { get; }
    public string Composer { get; }
    public string Duration { get; }
    public string Quality { get; }

    [ObservableProperty]
    private bool isSelected;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsPreviewIdle))]
    private bool isPreviewActive;

    [ObservableProperty]
    private bool isPreviewPlaying;

    public bool IsPreviewIdle => !IsPreviewActive;

    partial void OnIsSelectedChanged(bool value)
    {
        selectionChanged?.Invoke(this);
    }

    [RelayCommand]
    private Task PlayPreview()
    {
        return previewRequested?.Invoke(this) ?? Task.CompletedTask;
    }
}
