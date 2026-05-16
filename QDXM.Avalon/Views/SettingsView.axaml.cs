using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using QDXM.Avalon.ViewModels;

namespace QDXM.Avalon.Views;

public partial class SettingsView : UserControl
{
    public SettingsView()
    {
        InitializeComponent();
    }

    private async void BrowseDownloadFolder_OnClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not SettingsViewModel viewModel)
        {
            return;
        }

        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel is null)
        {
            return;
        }

        IReadOnlyList<IStorageFolder> folders;
        try
        {
            folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = "Select download folder",
                AllowMultiple = false
            });
        }
        catch (Exception)
        {
            viewModel.StatusText = "Folder picker unavailable.";
            return;
        }

        var selectedFolder = folders.Count > 0
            ? folders[0].TryGetLocalPath()
            : null;

        if (!string.IsNullOrWhiteSpace(selectedFolder))
        {
            viewModel.DownloadFolder = selectedFolder;
            viewModel.StatusText = "Download folder selected. Save settings to keep it.";
        }
    }

    private void MaxFileNameLength_OnTextInput(object? sender, TextInputEventArgs e)
    {
        if (e.Text?.All(char.IsDigit) != true)
        {
            e.Handled = true;
        }
    }
}
