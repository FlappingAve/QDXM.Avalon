namespace QDXM.Avalon.Core.Search;

public sealed record SearchArtistResult(
    string ArtistId,
    string Name,
    string Slug,
    string ThumbnailUrl,
    string WebPlayerUrl,
    int AlbumsCount);
