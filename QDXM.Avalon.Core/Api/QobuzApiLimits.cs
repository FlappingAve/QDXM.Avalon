namespace QDXM.Avalon.Core.Api;

public static class QobuzApiLimits
{
    // Observed Qobuz API page limits.
    public const int SearchPageSize = 500;
    public const int AlbumTrackPageSize = 500;
    public const int PlaylistTrackPageSize = 500;
    public const int FavoriteIdPageSize = 5000;
    public const int ArtistReleasePageSize = 100;

    // App-selected API request sizes.
    // These are not known Qobuz server limits; they control how much data the UI asks for at once.
    public const int PlaylistTrackPreviewPageSize = 100;

    // App safety guards.
    // Prevents runaway background pagination if Qobuz returns inconsistent page data.
    public const int MaxPaginationPages = 100;
}
