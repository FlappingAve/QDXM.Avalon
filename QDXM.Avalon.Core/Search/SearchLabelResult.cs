namespace QDXM.Avalon.Core.Search;

public sealed record SearchLabelResult(
    string LabelId,
    string Name,
    string Slug,
    string WebPlayerUrl,
    int AlbumsCount);
