using QobuzApiSharp.Models.Content;
using QDXM.Avalon.Core.Search;
using QDXM.Avalon.Core.Tools;

namespace QDXM.Avalon.Core.Api;

public static class QobuzApiSearchMapper
{
    public static SearchAlbumResult ToAlbumResult(Album album)
    {
        var tracks = album.Tracks?.Items?
            .Where(track => track is not null)
            .Select(ToAlbumTrack)
            .ToList() ?? [];

        return new SearchAlbumResult(
            AlbumId: album.Id ?? string.Empty,
            Title: QobuzTitleFormatter.AlbumTitle(album.Title),
            Version: album.Version ?? string.Empty,
            Artist: GetAlbumArtist(album),
            Quality: FormatQuality(album.MaximumBitDepth, album.MaximumSamplingRate),
            ReleaseDate: StringTools.FormatDateTimeOffset(album.ReleaseDateOriginal ?? album.ReleaseDateDownload ?? album.ReleaseDateStream),
            Upc: album.Upc,
            ThumbnailUrl: CoverArtUrlSelector.GetBestImageUrl(album.Image),
            WebPlayerUrl: QobuzUrlBuilder.CreateOpenUrl("album", album.Id),
            StoreUrl: GetStoreUrl(album.Url, album.RelativeUrl, album.ProductUrl),
            TotalTracks: album.TracksCount ?? album.Tracks?.Total ?? album.TrackIds?.Count ?? tracks.Count,
            TotalDiscs: album.MediaCount ?? tracks.Select(track => track.DiscNumber).DefaultIfEmpty(0).Max(),
            Explicit: album.ParentalWarning == true || tracks.Any(track => track.Explicit),
            Tracks: tracks);
    }

    public static SearchTrackResult ToTrackResult(Track track)
    {
        return new SearchTrackResult(
            TrackId: track.Id?.ToString() ?? string.Empty,
            Title: QobuzTitleFormatter.TrackTitle(track.Title),
            Version: track.Version ?? string.Empty,
            Artist: GetTrackArtist(track),
            AlbumTitle: QobuzTitleFormatter.AlbumTitle(track.Album?.Title),
            Quality: FormatQuality(
                track.MaximumBitDepth ?? track.Album?.MaximumBitDepth,
                track.MaximumSamplingRate ?? track.Album?.MaximumSamplingRate),
            Duration: TimeSpan.FromSeconds(track.Duration ?? 0),
            ReleaseDate: StringTools.FormatDateTimeOffset(
                track.ReleaseDateOriginal ??
                track.ReleaseDateDownload ??
                track.ReleaseDateStream ??
                track.Album?.ReleaseDateOriginal ??
                track.Album?.ReleaseDateDownload ??
                track.Album?.ReleaseDateStream),
            ThumbnailUrl: CoverArtUrlSelector.GetBestImageUrl(track.Album?.Image),
            WebPlayerUrl: QobuzUrlBuilder.CreateOpenUrl("track", track.Id?.ToString()),
            StoreUrl: GetStoreUrl(track.Album?.Url, track.Album?.RelativeUrl, track.Album?.ProductUrl),
            Explicit: track.ParentalWarning == true);
    }

    public static SearchPlaylistResult ToPlaylistResult(Playlist playlist)
    {
        var playlistId = playlist.Id?.ToString() ?? string.Empty;

        return new SearchPlaylistResult(
            PlaylistId: playlistId,
            Title: string.IsNullOrWhiteSpace(playlist.Name) ? $"Playlist {playlistId}" : playlist.Name.Trim(),
            Owner: string.IsNullOrWhiteSpace(playlist.Owner?.Name) ? "Unknown Owner" : playlist.Owner.Name.Trim(),
            UpdatedDate: FormatUnixDate(playlist.UpdatedAt),
            CreatedDate: FormatUnixDate(playlist.CreatedAt),
            Duration: TimeSpan.FromSeconds(playlist.Duration ?? 0),
            ThumbnailUrl: PlaylistImageUrlSelector.GetBestImageUrl(playlist) ?? string.Empty,
            WebPlayerUrl: QobuzUrlBuilder.CreateOpenUrl("playlist", playlistId),
            TotalTracks: playlist.TracksCount ?? playlist.Tracks?.Total ?? playlist.TrackIds?.Count ?? 0);
    }

    public static SearchPlaylistTrackPage ToPlaylistTrackPage(Playlist playlist, int offset)
    {
        var totalTracks = playlist.TracksCount ?? playlist.Tracks?.Total ?? playlist.TrackIds?.Count ?? 0;
        var paddingWidth = Math.Max(2, totalTracks.ToString().Length);
        var tracks = playlist.Tracks?.Items?
            .Where(track => track is not null)
            .Select((track, index) => ToPlaylistTrackResult(track, offset + index, totalTracks, paddingWidth))
            .ToList() ?? [];

        return new SearchPlaylistTrackPage(totalTracks, tracks);
    }

    public static SearchArtistResult ToArtistResult(Artist artist)
    {
        var artistId = artist.Id?.ToString() ?? string.Empty;

        return new SearchArtistResult(
            ArtistId: artistId,
            Name: artist.Name ?? string.Empty,
            Slug: artist.Slug ?? string.Empty,
            ThumbnailUrl: CoverArtUrlSelector.GetBestImageUrl(artist.Image, artist.Picture),
            WebPlayerUrl: QobuzUrlBuilder.CreateOpenUrl("artist", artistId),
            AlbumsCount: artist.AlbumsCount ?? artist.Albums?.Total ?? 0);
    }

    public static SearchAlbumResult ToAlbumResult(Release release)
    {
        return new SearchAlbumResult(
            AlbumId: release.Id ?? string.Empty,
            Title: QobuzTitleFormatter.AlbumTitle(release.Title),
            Version: release.Version ?? string.Empty,
            Artist: GetReleaseArtist(release),
            Quality: string.Empty,
            ReleaseDate: StringTools.FormatDateTimeOffset(release.Dates?.Original ?? release.Dates?.Download ?? release.Dates?.Stream),
            Upc: null,
            ThumbnailUrl: CoverArtUrlSelector.GetBestImageUrl(release.Image),
            WebPlayerUrl: QobuzUrlBuilder.CreateOpenUrl("album", release.Id),
            StoreUrl: string.Empty,
            TotalTracks: (int)(release.TracksCount ?? release.Tracks?.Items?.Count ?? 0),
            TotalDiscs: 0,
            Explicit: release.ParentalWarning == true,
            Tracks: []);
    }

    public static SearchAlbumTrack ToAlbumTrack(Track track)
    {
        return new SearchAlbumTrack(
            TrackId: track.Id?.ToString() ?? string.Empty,
            TrackNumber: track.TrackNumber ?? 0,
            DiscNumber: track.MediaNumber ?? 0,
            Title: QobuzTitleFormatter.TrackTitle(track.Title),
            Version: track.Version ?? string.Empty,
            Work: track.Work ?? string.Empty,
            Composer: track.Composer?.Name ?? string.Empty,
            Duration: TimeSpan.FromSeconds(track.Duration ?? 0),
            Quality: FormatQuality(
                track.MaximumBitDepth ?? track.Album?.MaximumBitDepth,
                track.MaximumSamplingRate ?? track.Album?.MaximumSamplingRate),
            Explicit: track.ParentalWarning == true);
    }

    private static SearchPlaylistTrackResult ToPlaylistTrackResult(
        Track track,
        int returnedIndex,
        int playlistTotalTracks,
        int playlistPaddingWidth)
    {
        var playlistPosition = track.PlaylistPosition is > 0
            ? track.PlaylistPosition.Value
            : returnedIndex + 1;

        return new SearchPlaylistTrackResult(
            TrackId: track.Id?.ToString() ?? string.Empty,
            SelectionKey: PlaylistTrackSelectionKey.Create(track.PlaylistTrackId, track.PlaylistPosition, returnedIndex),
            PlaylistPosition: playlistPosition,
            PlaylistPositionDisplay: playlistPosition.ToString($"D{playlistPaddingWidth}"),
            AlbumTrackNumber: track.TrackNumber ?? 0,
            AlbumDiscNumber: track.MediaNumber ?? 0,
            AlbumPositionDisplay: FormatAlbumTrackPosition(track),
            Title: QobuzTitleFormatter.TrackTitle(track.Title),
            Version: track.Version ?? string.Empty,
            Artist: GetTrackArtist(track),
            AlbumTitle: QobuzTitleFormatter.AlbumTitle(track.Album?.Title),
            Duration: TimeSpan.FromSeconds(track.Duration ?? 0));
    }

    private static string GetAlbumArtist(Album album)
    {
        if (!string.IsNullOrWhiteSpace(album.Artist?.Name))
        {
            return album.Artist.Name;
        }

        if (album.Artists is { Count: > 0 })
        {
            return string.Join(", ", album.Artists
                .Select(artist => artist?.Name)
                .Where(name => !string.IsNullOrWhiteSpace(name)));
        }

        return string.Empty;
    }

    private static string GetTrackArtist(Track track)
    {
        if (!string.IsNullOrWhiteSpace(track.Performer?.Name))
        {
            return track.Performer.Name;
        }

        if (!string.IsNullOrWhiteSpace(track.Album?.Artist?.Name))
        {
            return track.Album.Artist.Name;
        }

        return track.Composer?.Name ?? string.Empty;
    }

    private static string GetReleaseArtist(Release release)
    {
        if (!string.IsNullOrWhiteSpace(release.Artist?.Name?.Display))
        {
            return release.Artist.Name.Display;
        }

        if (release.Artists is { Count: > 0 })
        {
            return string.Join(", ", release.Artists
                .Select(artist => artist?.Name)
                .Where(name => !string.IsNullOrWhiteSpace(name)));
        }

        return string.Empty;
    }

    private static string FormatQuality(double? bitDepth, double? samplingRate)
    {
        if (bitDepth is null || samplingRate is null)
        {
            return string.Empty;
        }

        return $"FLAC {StringTools.FormatWholeOrSingleDecimal(bitDepth.Value)}/{StringTools.FormatWholeOrSingleDecimal(samplingRate.Value)}";
    }

    private static string FormatAlbumTrackPosition(Track track)
    {
        var trackNumber = track.TrackNumber ?? 0;
        if (trackNumber <= 0)
        {
            return string.Empty;
        }

        var trackText = trackNumber.ToString("00");
        var discNumber = track.MediaNumber ?? 0;
        if (discNumber > 1 || track.Album?.MediaCount > 1)
        {
            return $"{Math.Max(1, discNumber)}-{trackText}";
        }

        return trackText;
    }

    private static string FormatUnixDate(long? seconds)
    {
        return seconds is null or <= 0
            ? string.Empty
            : DateTimeOffset.FromUnixTimeSeconds(seconds.Value).ToString("yyyy-MM-dd");
    }

    private static string GetStoreUrl(string? url, string? relativeUrl, string? productUrl)
    {
        if (!string.IsNullOrWhiteSpace(url))
        {
            return url;
        }

        if (!string.IsNullOrWhiteSpace(productUrl))
        {
            return productUrl;
        }

        if (string.IsNullOrWhiteSpace(relativeUrl))
        {
            return string.Empty;
        }

        return relativeUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase)
            ? relativeUrl
            : $"https://www.qobuz.com{relativeUrl}";
    }
}
