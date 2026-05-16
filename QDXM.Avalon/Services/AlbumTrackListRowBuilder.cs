using QDXM.Avalon.ViewModels;

namespace QDXM.Avalon.Services;

public static class AlbumTrackListRowBuilder
{
    public static IReadOnlyList<AlbumTrackListRowViewModel> Build(
        IReadOnlyList<AlbumTrackSelectionViewModel> tracks,
        int totalDiscs)
    {
        if (tracks.Count == 0)
        {
            return [];
        }

        var effectiveTotalDiscs = Math.Max(
            totalDiscs,
            tracks.Select(track => track.DiscNumber).DefaultIfEmpty(0).Max());

        if (effectiveTotalDiscs <= 1)
        {
            return tracks.Select(AlbumTrackListRowViewModel.TrackRow).ToList();
        }

        var rows = new List<AlbumTrackListRowViewModel>();
        var previousDiscNumber = int.MinValue;
        var previousWork = string.Empty;

        foreach (var track in tracks)
        {
            var discNumber = track.DiscNumber > 0 ? track.DiscNumber : 1;
            if (discNumber != previousDiscNumber)
            {
                rows.Add(AlbumTrackListRowViewModel.DiscHeader(discNumber));
                previousDiscNumber = discNumber;
                previousWork = string.Empty;
            }

            var work = track.Work.Trim();
            if (!string.IsNullOrWhiteSpace(work) &&
                !string.Equals(work, previousWork, StringComparison.OrdinalIgnoreCase))
            {
                rows.Add(AlbumTrackListRowViewModel.WorkHeader(work, track.Composer.Trim()));
                previousWork = work;
            }
            else if (string.IsNullOrWhiteSpace(work))
            {
                previousWork = string.Empty;
            }

            rows.Add(AlbumTrackListRowViewModel.TrackRow(track));
        }

        return rows;
    }
}
