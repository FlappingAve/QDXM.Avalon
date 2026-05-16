using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using QDXM.Avalon.ViewModels;
using System.Text;

namespace QDXM.Avalon.Views;

public partial class LogsView : UserControl
{
    public LogsView()
    {
        InitializeComponent();
    }

    private async void CopySelectedLogRows_OnClick(object? sender, RoutedEventArgs e)
    {
        var entries = GetSelectedLogEntries();
        if (entries.Count == 0)
        {
            return;
        }

        await CopyToClipboardAsync(FormatLogRows(entries, includeHeader: true));
    }

    private async void CopySelectedLogMessages_OnClick(object? sender, RoutedEventArgs e)
    {
        var entries = GetSelectedLogEntries();
        if (entries.Count == 0)
        {
            return;
        }

        await CopyToClipboardAsync(string.Join(Environment.NewLine, entries.Select(entry => entry.Message)));
    }

    private async void CopyAllLogRows_OnClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not LogsViewModel viewModel || viewModel.Entries.Count == 0)
        {
            return;
        }

        await CopyToClipboardAsync(FormatLogRows(viewModel.Entries, includeHeader: true));
    }

    private IReadOnlyList<LogEntryViewModel> GetSelectedLogEntries()
    {
        var entries = LogsGrid.SelectedItems
            .OfType<LogEntryViewModel>()
            .ToList();

        if (entries.Count == 0 && LogsGrid.SelectedItem is LogEntryViewModel selectedEntry)
        {
            entries.Add(selectedEntry);
        }

        return entries;
    }

    private static string FormatLogRows(IEnumerable<LogEntryViewModel> entries, bool includeHeader)
    {
        var builder = new StringBuilder();
        if (includeHeader)
        {
            builder.AppendLine("Time\tLevel\tSource\tMessage");
        }

        foreach (var entry in entries)
        {
            builder
                .Append(CleanClipboardCell(entry.TimestampText))
                .Append('\t')
                .Append(CleanClipboardCell(entry.Level))
                .Append('\t')
                .Append(CleanClipboardCell(entry.Source))
                .Append('\t')
                .AppendLine(CleanClipboardCell(entry.Message));
        }

        return builder.ToString().TrimEnd();
    }

    private static string CleanClipboardCell(string value)
    {
        return value
            .Replace('\t', ' ')
            .Replace("\r\n", " ", StringComparison.Ordinal)
            .Replace('\r', ' ')
            .Replace('\n', ' ');
    }

    private async Task CopyToClipboardAsync(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
        if (clipboard is not null)
        {
            await clipboard.SetTextAsync(text);
        }
    }
}
