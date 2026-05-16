using Avalonia.Media;
using QDXM.Avalon.Services;

namespace QDXM.Avalon.ViewModels;

public sealed class LogEntryViewModel
{
    private readonly AppLogEntry entry;

    public LogEntryViewModel(AppLogEntry entry)
    {
        this.entry = entry;
    }

    public string TimestampText => entry.Timestamp.ToString("yyyy-MM-dd HH:mm:ss");
    public string Level => entry.Level;
    public string Source => entry.Source;
    public string Message => entry.Message;

    public IBrush LevelBrush => Level switch
    {
        "Error" => Brushes.IndianRed,
        "Warning" => Brushes.Yellow,
        _ => Brushes.LightSlateGray
    };
}
