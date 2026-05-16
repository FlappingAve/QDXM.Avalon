namespace QDXM.Avalon.ViewModels;

public sealed class AlbumTrackListRowViewModel
{
    private AlbumTrackListRowViewModel(
        AlbumTrackListRowKind kind,
        string headerText,
        AlbumTrackSelectionViewModel? track)
    {
        Kind = kind;
        HeaderText = headerText;
        Track = track;
    }

    public AlbumTrackListRowKind Kind { get; }
    public string HeaderText { get; }
    public AlbumTrackSelectionViewModel? Track { get; }
    public bool IsTrack => Kind == AlbumTrackListRowKind.Track;
    public bool IsDiscHeader => Kind == AlbumTrackListRowKind.DiscHeader;
    public bool IsWorkHeader => Kind == AlbumTrackListRowKind.WorkHeader;

    public static AlbumTrackListRowViewModel DiscHeader(int discNumber)
    {
        return new AlbumTrackListRowViewModel(
            AlbumTrackListRowKind.DiscHeader,
            $"Disc {discNumber}",
            null);
    }

    public static AlbumTrackListRowViewModel WorkHeader(string work, string composer)
    {
        var header = string.IsNullOrWhiteSpace(composer)
            ? work
            : $"{work} ({composer})";

        return new AlbumTrackListRowViewModel(AlbumTrackListRowKind.WorkHeader, header, null);
    }

    public static AlbumTrackListRowViewModel TrackRow(AlbumTrackSelectionViewModel track)
    {
        return new AlbumTrackListRowViewModel(AlbumTrackListRowKind.Track, string.Empty, track);
    }
}

public enum AlbumTrackListRowKind
{
    DiscHeader,
    WorkHeader,
    Track
}
