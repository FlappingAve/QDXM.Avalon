using System.Text;
using QobuzApiSharp.Models.Content;
using QDXM.Avalon.Core.Downloads;
using QDXM.Avalon.Core.Settings;
using QDXM.Avalon.Core.Tools;
using QDXM.Avalon.ViewModels;

namespace QDXM.Avalon.Services;

public static class DestinationPreviewRenderer
{
    private const int PlaylistPreviewSamplePathCount = 2;

    public static string ForSearchResult(SearchResultViewModel result, AppSettings settings)
    {
        if (result.IsPlaylist)
        {
            return ForPlaylistSearchResult(result, settings);
        }

        var baseFolder = GetBaseFolder(settings);
        var destinationAlbumTitle = result.IsAlbum || string.IsNullOrWhiteSpace(result.AlbumTitle)
            ? result.Title
            : result.AlbumTitle;
        var albumDestination = PathTemplateRenderer.RenderAlbumDestinationPreview(
            baseFolder,
            settings.FolderTemplate,
            result.Artist,
            destinationAlbumTitle,
            result.Quality,
            result.ReleaseDate,
            result.TotalTracks,
            version: result.Version);
        var folderSegments = StringTools.GetRelativeSegments(baseFolder, albumDestination);
        var selectedTracks = result.Tracks
            .Where(track => track.IsSelected)
            .OrderBy(track => track.DiscNumber <= 0 ? 1 : track.DiscNumber)
            .ThenBy(track => track.TrackNumber <= 0 ? int.MaxValue : track.TrackNumber)
            .ToList();
        var trackNumberScopes = GetTrackNumberScopes(selectedTracks);

        var builder = CreateFolderTree(baseFolder, folderSegments);
        var trackAncestors = GetTerminalAncestors(folderSegments.Count);

        if (!result.IsAlbum)
        {
            var trackFileName = PathTemplateRenderer.RenderAudioFilenamePreview(
                settings.FilenameTemplate,
                result.Artist,
                destinationAlbumTitle,
                result.Quality,
                result.ReleaseDate,
                totalTracks: 1,
                trackNumber: 1,
                trackTitle: result.Title,
                version: result.Version,
                discNumber: 0,
                totalDiscs: 1,
                QualityStringMappings.GetAudioExtension(settings.FormatId),
                settings.MaxFileNameLength);

            StringTools.AppendTreeLeaf(builder, trackAncestors, trackFileName, isLast: true);
            return builder.ToString().TrimEnd();
        }

        if (selectedTracks.Count == 0)
        {
            var countText = result.Tracks.Count > 0
                ? "0 selected tracks"
                : result.TotalTracks > 0
                    ? $"{result.TotalTracks} tracks"
                    : "Tracks load after expanding";
            StringTools.AppendTreeLeaf(builder, trackAncestors, countText, isLast: true);
            return builder.ToString().TrimEnd();
        }

        var sampleTrack = selectedTracks[0];
        var totalDiscs = Math.Max(result.TotalDiscs, result.Tracks.Select(track => track.DiscNumber).DefaultIfEmpty(0).Max());
        if (totalDiscs > 1 && sampleTrack.DiscNumber > 0 && !string.IsNullOrWhiteSpace(settings.DiscFolderTemplate))
        {
            AppendDiscPreview(
                builder,
                result,
                selectedTracks,
                trackNumberScopes,
                totalDiscs,
                trackAncestors,
                settings);
            return builder.ToString().TrimEnd();
        }

        var fileName = RenderTrackFileName(result, sampleTrack, selectedTracks.Count, totalDiscs, trackNumberScopes, settings);
        var moreCount = selectedTracks.Count - 1;

        StringTools.AppendTreeLeaf(builder, trackAncestors, fileName, isLast: moreCount == 0);
        if (moreCount > 0)
        {
            StringTools.AppendTreeLeaf(builder, trackAncestors, $"{moreCount} more", isLast: true);
        }

        return builder.ToString().TrimEnd();
    }

    private static string ForPlaylistSearchResult(SearchResultViewModel result, AppSettings settings)
    {
        var baseFolder = GetBaseFolder(settings);
        var selectedTracks = result.SelectedTracksForPreview;
        var totalTracks = selectedTracks.Count > 0 ? selectedTracks.Count : result.TotalTracks;
        var sampleTrack = selectedTracks.FirstOrDefault();
        var playlistDestination = PathTemplateRenderer.RenderPlaylistDestination(
            baseFolder,
            settings.PlaylistFolderTemplate,
            result.Id,
            result.Title,
            result.Artist,
            sampleTrack is null ? null : CreatePreviewTrack(sampleTrack),
            sampleTrack is null ? null : CreatePreviewAlbum(sampleTrack),
            sampleTrack?.Artist ?? string.Empty,
            sampleTrack?.AlbumTitle ?? string.Empty,
            string.Empty,
            playlistNumber: 1,
            playlistTotalTracks: totalTracks);
        var folderSegments = StringTools.GetRelativeSegments(baseFolder, playlistDestination);
        var builder = CreateFolderTree(baseFolder, folderSegments);
        var trackAncestors = GetTerminalAncestors(folderSegments.Count);

        if (selectedTracks.Count == 0)
        {
            var countText = result.TotalTracks > 0
                ? $"{result.TotalTracks} tracks"
                : "Tracks load after expanding";
            StringTools.AppendTreeLeaf(builder, trackAncestors, countText, isLast: true);
            return builder.ToString().TrimEnd();
        }

        var previewTracks = selectedTracks.Take(PlaylistPreviewSamplePathCount).ToList();
        var moreCount = selectedTracks.Count - previewTracks.Count;
        for (var index = 0; index < previewTracks.Count; index++)
        {
            var track = previewTracks[index];
            var previewTrack = CreatePreviewTrack(track);
            var previewAlbum = CreatePreviewAlbum(track);
            var fileName = PathTemplateRenderer.RenderPlaylistAudioFilename(
                settings.PlaylistFilenameTemplate,
                previewTrack,
                previewAlbum,
                track.Artist,
                track.AlbumTitle,
                string.Empty,
                result.Id,
                result.Title,
                result.Artist,
                playlistNumber: index + 1,
                playlistTotalTracks: totalTracks,
                QualityStringMappings.GetAudioExtension(settings.FormatId),
                settings.MaxFileNameLength);
            StringTools.AppendTreeLeaf(
                builder,
                trackAncestors,
                fileName,
                isLast: moreCount == 0 && index == previewTracks.Count - 1);
        }

        if (moreCount > 0)
        {
            StringTools.AppendTreeLeaf(builder, trackAncestors, $"{moreCount} more", isLast: true);
        }

        return builder.ToString().TrimEnd();
    }

    public static string ForDownloadItem(DownloadQueueItemViewModel item, AppSettings settings)
    {
        var baseFolder = GetBaseFolder(settings);
        var destination = string.IsNullOrWhiteSpace(item.DestinationPath)
            ? baseFolder
            : item.DestinationPath;
        var filePaths = item.DestinationFilePaths
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .ToList();

        if (filePaths.Count > 0)
        {
            if (item.Type == DownloadContentType.Playlist || item.DestinationPreviewRemainingCount > 0)
            {
                return BuildCompactFilePathPreview(baseFolder, filePaths, item.DestinationPreviewRemainingCount);
            }

            return BuildFilePathPreview(baseFolder, filePaths);
        }

        return BuildFolderPreview(baseFolder, destination);
    }

    private static void AppendDiscPreview(
        StringBuilder builder,
        SearchResultViewModel result,
        IReadOnlyList<AlbumTrackSelectionViewModel> selectedTracks,
        IReadOnlyList<(int TrackNumber, int DiscNumber)> trackNumberScopes,
        int totalDiscs,
        IReadOnlyList<bool> trackAncestors,
        AppSettings settings)
    {
        var sampleTrack = selectedTracks[0];
        var selectedDiscGroups = selectedTracks
            .Where(track => track.DiscNumber > 0)
            .GroupBy(track => track.DiscNumber)
            .OrderBy(group => group.Key)
            .ToList();

        for (var index = 0; index < selectedDiscGroups.Count; index++)
        {
            var group = selectedDiscGroups[index];
            var firstTrackInDisc = group.First();
            var discSegments = PathTemplateRenderer.RenderDiscFolderSegmentsPreview(
                settings.DiscFolderTemplate,
                settings.DiscWorkHandling,
                settings.DiscWorkSeparator,
                settings.DiscWorkSeparatorNoSpaces,
                result.Artist,
                result.Title,
                string.IsNullOrWhiteSpace(firstTrackInDisc.Quality) ? result.Quality : firstTrackInDisc.Quality,
                result.ReleaseDate,
                selectedTracks.Count,
                firstTrackInDisc.TrackNumber,
                firstTrackInDisc.Title,
                result.Version,
                firstTrackInDisc.DiscNumber,
                totalDiscs,
                group.Select(track => track.Work).ToArray(),
                firstTrackInDisc.Work);

            if (discSegments.Count == 0)
            {
                continue;
            }

            var currentAncestors = trackAncestors.ToList();
            var isLastDisc = index == selectedDiscGroups.Count - 1;
            for (var segmentIndex = 0; segmentIndex < discSegments.Count; segmentIndex++)
            {
                var isLastSegment = segmentIndex == 0
                    ? isLastDisc
                    : true;
                StringTools.AppendTreeLeaf(
                    builder,
                    currentAncestors,
                    discSegments[segmentIndex],
                    isLastSegment);
                currentAncestors.Add(!isLastSegment);
            }

            if (group.Key != sampleTrack.DiscNumber)
            {
                continue;
            }

            var sampleFileName = RenderTrackFileName(result, sampleTrack, selectedTracks.Count, totalDiscs, trackNumberScopes, settings);
            var moreInDiscCount = group.Count() - 1;

            StringTools.AppendTreeLeaf(builder, currentAncestors, sampleFileName, isLast: moreInDiscCount == 0);
            if (moreInDiscCount > 0)
            {
                StringTools.AppendTreeLeaf(builder, currentAncestors, $"{moreInDiscCount} more", isLast: true);
            }
        }
    }

    private static string RenderTrackFileName(
        SearchResultViewModel result,
        AlbumTrackSelectionViewModel track,
        int selectedTrackCount,
        int totalDiscs,
        IReadOnlyList<(int TrackNumber, int DiscNumber)> trackNumberScopes,
        AppSettings settings)
    {
        return PathTemplateRenderer.RenderAudioFilenamePreview(
            settings.FilenameTemplate,
            result.Artist,
            result.Title,
            string.IsNullOrWhiteSpace(track.Quality) ? result.Quality : track.Quality,
            result.ReleaseDate,
            selectedTrackCount,
            track.TrackNumber,
            track.Title,
            track.Version,
            track.DiscNumber,
            totalDiscs,
            QualityStringMappings.GetAudioExtension(settings.FormatId),
            settings.MaxFileNameLength,
            trackNumberPaddingWidth: PathTemplateRenderer.GetTrackNumberPaddingWidth(
            trackNumberScopes,
            track.DiscNumber,
            settings.DiscFolderTemplate));
    }

    private static Track CreatePreviewTrack(AlbumTrackSelectionViewModel track)
    {
        return new Track
        {
            Title = track.Title,
            Version = track.Version,
            Performer = new Artist { Name = track.Artist },
            TrackNumber = track.AlbumTrackNumber,
            MediaNumber = track.AlbumDiscNumber
        };
    }

    private static Album CreatePreviewAlbum(AlbumTrackSelectionViewModel track)
    {
        return new Album
        {
            Title = track.AlbumTitle,
            Artist = new Artist { Name = track.Artist }
        };
    }

    private static string BuildFolderPreview(string baseFolder, string destination)
    {
        var folderSegments = StringTools.GetRelativeSegments(baseFolder, destination);
        return CreateFolderTree(baseFolder, folderSegments).ToString().TrimEnd();
    }

    private static StringBuilder CreateFolderTree(string baseFolder, IReadOnlyList<string> folderSegments)
    {
        var builder = new StringBuilder();
        builder.AppendLine(baseFolder);

        for (var index = 0; index < folderSegments.Count; index++)
        {
            StringTools.AppendTreeLeaf(builder, GetTerminalAncestors(index), folderSegments[index], isLast: true);
        }

        return builder;
    }

    private static string BuildFilePathPreview(string baseFolder, IReadOnlyList<string> filePaths)
    {
        var root = new DestinationPreviewNode();
        foreach (var filePath in filePaths)
        {
            var segments = StringTools.GetRelativeSegments(baseFolder, filePath);
            if (segments.Count == 0)
            {
                continue;
            }

            var current = root;
            foreach (var segment in segments.Take(segments.Count - 1))
            {
                current = current.GetOrAddFolder(segment);
            }

            current.Files.Add(segments[^1]);
        }

        var builder = new StringBuilder();
        builder.AppendLine(baseFolder);
        var sampleFileWritten = false;
        AppendPreviewNode(builder, root, [], ref sampleFileWritten);
        return builder.ToString().TrimEnd();
    }

    private static string BuildCompactFilePathPreview(
        string baseFolder,
        IReadOnlyList<string> sampleFilePaths,
        int remainingCount)
    {
        var root = new DestinationPreviewNode();
        var sampleSegments = new List<IReadOnlyList<string>>();
        foreach (var filePath in sampleFilePaths)
        {
            var segments = StringTools.GetRelativeSegments(baseFolder, filePath);
            if (segments.Count == 0)
            {
                continue;
            }

            sampleSegments.Add(segments);
            AddPathToPreviewTree(root, segments);
        }

        if (remainingCount > 0)
        {
            var commonFolderSegments = GetCommonFolderSegments(sampleSegments);
            var remainingNode = GetOrAddNode(root, commonFolderSegments);
            remainingNode.Files.Add($"{remainingCount} more");
        }

        var builder = new StringBuilder();
        builder.AppendLine(baseFolder);
        AppendCompactPreviewNode(builder, root, []);
        return builder.ToString().TrimEnd();
    }

    private static void AddPathToPreviewTree(DestinationPreviewNode root, IReadOnlyList<string> segments)
    {
        var current = root;
        foreach (var segment in segments.Take(segments.Count - 1))
        {
            current = current.GetOrAddFolder(segment);
        }

        current.Files.Add(segments[^1]);
    }

    private static DestinationPreviewNode GetOrAddNode(DestinationPreviewNode root, IReadOnlyList<string> folderSegments)
    {
        var current = root;
        foreach (var segment in folderSegments)
        {
            current = current.GetOrAddFolder(segment);
        }

        return current;
    }

    private static IReadOnlyList<string> GetCommonFolderSegments(IReadOnlyList<IReadOnlyList<string>> sampleSegments)
    {
        if (sampleSegments.Count == 0)
        {
            return [];
        }

        var folderSegments = sampleSegments
            .Select(segments => segments.Take(Math.Max(0, segments.Count - 1)).ToArray())
            .ToArray();
        var shortestLength = folderSegments.Min(segments => segments.Length);
        var common = new List<string>();

        for (var index = 0; index < shortestLength; index++)
        {
            var segment = folderSegments[0][index];
            if (folderSegments.Any(segments => !string.Equals(segments[index], segment, StringComparison.OrdinalIgnoreCase)))
            {
                break;
            }

            common.Add(segment);
        }

        return common;
    }

    private static void AppendPreviewNode(
        StringBuilder builder,
        DestinationPreviewNode node,
        IReadOnlyList<bool> ancestors,
        ref bool sampleFileWritten)
    {
        var folders = node.Folders.Values.ToList();
        for (var index = 0; index < folders.Count; index++)
        {
            var folder = folders[index];
            var hasVisibleFile = node.Files.Count > 0 && !sampleFileWritten;
            var isLast = index == folders.Count - 1 && !hasVisibleFile;
            StringTools.AppendTreeLeaf(builder, ancestors, folder.Name, isLast);
            AppendPreviewNode(builder, folder, GetChildAncestors(ancestors, isLast), ref sampleFileWritten);
        }

        if (node.Files.Count == 0 || sampleFileWritten)
        {
            return;
        }

        StringTools.AppendTreeLeaf(builder, ancestors, node.Files[0], isLast: node.Files.Count == 1);
        var moreCount = node.Files.Count - 1;
        if (moreCount > 0)
        {
            StringTools.AppendTreeLeaf(builder, ancestors, $"{moreCount} more", isLast: true);
        }

        sampleFileWritten = true;
    }

    private static void AppendCompactPreviewNode(
        StringBuilder builder,
        DestinationPreviewNode node,
        IReadOnlyList<bool> ancestors)
    {
        var folders = node.Folders.Values.ToList();
        for (var index = 0; index < folders.Count; index++)
        {
            var folder = folders[index];
            var isLast = index == folders.Count - 1 && node.Files.Count == 0;
            StringTools.AppendTreeLeaf(builder, ancestors, folder.Name, isLast);
            AppendCompactPreviewNode(builder, folder, GetChildAncestors(ancestors, isLast));
        }

        for (var index = 0; index < node.Files.Count; index++)
        {
            StringTools.AppendTreeLeaf(builder, ancestors, node.Files[index], isLast: index == node.Files.Count - 1);
        }
    }

    private static List<bool> GetTerminalAncestors(int level)
    {
        return Enumerable.Repeat(false, Math.Max(0, level)).ToList();
    }

    private static List<bool> GetChildAncestors(IReadOnlyList<bool> ancestors, bool parentIsLast)
    {
        return [.. ancestors, !parentIsLast];
    }

    private static string GetBaseFolder(AppSettings settings)
    {
        return settings.EffectiveDownloadFolder;
    }

    private static IReadOnlyList<(int TrackNumber, int DiscNumber)> GetTrackNumberScopes(IReadOnlyList<AlbumTrackSelectionViewModel> tracks)
    {
        return tracks
            .Select(track => (track.TrackNumber, track.DiscNumber))
            .ToList();
    }

    private sealed class DestinationPreviewNode
    {
        public string Name { get; init; } = string.Empty;
        public Dictionary<string, DestinationPreviewNode> Folders { get; } = new(StringComparer.OrdinalIgnoreCase);
        public List<string> Files { get; } = [];

        public DestinationPreviewNode GetOrAddFolder(string name)
        {
            if (!Folders.TryGetValue(name, out var folder))
            {
                folder = new DestinationPreviewNode { Name = name };
                Folders[name] = folder;
            }

            return folder;
        }
    }
}
