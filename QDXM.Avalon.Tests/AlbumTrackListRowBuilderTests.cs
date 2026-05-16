using QDXM.Avalon.Services;
using QDXM.Avalon.ViewModels;

namespace QDXM.Avalon.Tests;

public sealed class AlbumTrackListRowBuilderTests
{
    [Fact]
    public void Build_KeepsSingleDiscAlbumsFlat()
    {
        var rows = AlbumTrackListRowBuilder.Build(
            [
                CreateTrack("1", discNumber: 1, work: "Work 1", composer: "Composer 1"),
                CreateTrack("2", trackNumber: 2, discNumber: 1, work: "Work 2", composer: "Composer 2")
            ],
            totalDiscs: 1);

        Assert.All(rows, row => Assert.True(row.IsTrack));
        Assert.Equal(["1", "2"], rows.Select(row => row.Track?.TrackId));
    }

    [Fact]
    public void Build_AddsUnpaddedDiscHeadersForEveryDiscOnMultidiscAlbums()
    {
        var rows = AlbumTrackListRowBuilder.Build(
            [
                CreateTrack("1", discNumber: 1),
                CreateTrack("2", discNumber: 2)
            ],
            totalDiscs: 2);

        Assert.Equal(
            ["Disc 1", "1", "Disc 2", "2"],
            rows.Select(row => row.IsTrack ? row.Track?.TrackId : row.HeaderText));
    }

    [Fact]
    public void Build_AddsWorkHeadersUsingFirstTrackComposerWithinEachWorkGroup()
    {
        var rows = AlbumTrackListRowBuilder.Build(
            [
                CreateTrack("1", discNumber: 1, work: "Work A", composer: "First Composer"),
                CreateTrack("2", trackNumber: 2, discNumber: 1, work: "Work A", composer: "Second Composer"),
                CreateTrack("3", trackNumber: 3, discNumber: 1, work: "Work B", composer: "Third Composer")
            ],
            totalDiscs: 2);

        Assert.Equal(
            ["Disc 1", "Work A (First Composer)", "1", "2", "Work B (Third Composer)", "3"],
            rows.Select(row => row.IsTrack ? row.Track?.TrackId : row.HeaderText));
    }

    private static AlbumTrackSelectionViewModel CreateTrack(
        string id,
        int trackNumber = 1,
        int discNumber = 1,
        string work = "",
        string composer = "")
    {
        return new AlbumTrackSelectionViewModel(
            id,
            trackNumber,
            discNumber,
            $"Track {id}",
            version: string.Empty,
            work,
            composer,
            duration: "1:00",
            quality: "FLAC 16/44.1",
            isSelected: true);
    }
}
