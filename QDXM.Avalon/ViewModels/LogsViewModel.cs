using System.Collections.ObjectModel;
using System.Collections.Specialized;
using CommunityToolkit.Mvvm.Input;
using QDXM.Avalon.Services;

namespace QDXM.Avalon.ViewModels;

public partial class LogsViewModel : ViewModelBase
{
    private readonly AppLogService logService;

    public LogsViewModel()
        : this(new AppLogService())
    {
    }

    public LogsViewModel(AppLogService logService)
    {
        this.logService = logService;
        Entries = new ObservableCollection<LogEntryViewModel>(
            logService.Entries.Select(entry => new LogEntryViewModel(entry)));
        logService.Entries.CollectionChanged += EntriesCollectionChanged;
    }

    public ObservableCollection<LogEntryViewModel> Entries { get; }

    public string EntryCountText => Entries.Count == 1 ? "1 entry" : $"{Entries.Count} entries";

    [RelayCommand]
    private void Clear()
    {
        logService.Clear();
    }

    private void EntriesCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action == NotifyCollectionChangedAction.Reset)
        {
            Entries.Clear();
            OnPropertyChanged(nameof(EntryCountText));
            return;
        }

        if (e.OldStartingIndex >= 0 && e.OldItems is not null)
        {
            for (var index = 0; index < e.OldItems.Count; index++)
            {
                Entries.RemoveAt(e.OldStartingIndex);
            }
        }

        if (e.NewStartingIndex >= 0 && e.NewItems is not null)
        {
            var insertIndex = e.NewStartingIndex;
            foreach (AppLogEntry entry in e.NewItems)
            {
                Entries.Insert(insertIndex++, new LogEntryViewModel(entry));
            }
        }

        OnPropertyChanged(nameof(EntryCountText));
    }
}
