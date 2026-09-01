namespace QDXM.Avalon.Services;

public static class SearchResultDisplayText
{
    public static string FormatTrackCount(int tracks)
    {
        if (tracks <= 0)
        {
            return string.Empty;
        }

        return tracks == 1 ? "1 track" : $"{tracks} tracks";
    }
}
