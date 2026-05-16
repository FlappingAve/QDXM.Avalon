using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using QDXM.Avalon.ViewModels;
using QDXM.Avalon.Core.Downloads;

namespace QDXM.Avalon.Views;

public partial class DownloadsView : UserControl
{
    public DownloadsView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) =>
        {
            if (DataContext is DownloadsViewModel viewModel)
            {
                viewModel.ConfirmRemovalAsync = ConfirmRemovalAsync;
                viewModel.ConfirmClearCompletedAsync = ConfirmClearCompletedAsync;
            }
        };
    }

    private void UrlTextBox_OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter ||
            DataContext is not DownloadsViewModel viewModel ||
            !viewModel.DownloadUrlCommand.CanExecute(null))
        {
            return;
        }

        viewModel.DownloadUrlCommand.Execute(null);
        e.Handled = true;
    }

    private async Task<bool> ConfirmRemovalAsync(DownloadQueueItemViewModel item)
    {
        var owner = TopLevel.GetTopLevel(this) as Window;
        if (owner is null)
        {
            return false;
        }

        var result = false;
        var dialog = new Window
        {
            Title = item.Status is DownloadStatus.Resolving or DownloadStatus.Downloading
                ? "Remove active download?"
                : "Remove download?",
            Width = 420,
            Height = 190,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false
        };

        dialog.Content = BuildConfirmContent(
            item,
            onRemove: () =>
            {
                result = true;
                dialog.Close();
            },
            closeDialog: dialog.Close);
        RegisterConfirmKeys(
            dialog,
            onConfirm: () =>
            {
                result = true;
                dialog.Close();
            },
            onCancel: dialog.Close);

        await dialog.ShowDialog(owner);
        return result;
    }

    private async Task<bool> ConfirmClearCompletedAsync(int completedCount)
    {
        var owner = TopLevel.GetTopLevel(this) as Window;
        if (owner is null)
        {
            return false;
        }

        var result = false;
        var dialog = new Window
        {
            Title = "Clear completed downloads?",
            Width = 420,
            Height = 170,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false
        };

        dialog.Content = BuildConfirmContent(
            $"Remove {completedCount} completed download{(completedCount == 1 ? string.Empty : "s")} from the queue?",
            "Completed files on disk will not be deleted.",
            "Clear",
            onConfirm: () =>
            {
                result = true;
                dialog.Close();
            },
            closeDialog: dialog.Close);
        RegisterConfirmKeys(
            dialog,
            onConfirm: () =>
            {
                result = true;
                dialog.Close();
            },
            onCancel: dialog.Close);

        await dialog.ShowDialog(owner);
        return result;
    }

    private static Control BuildConfirmContent(
        DownloadQueueItemViewModel item,
        Action onRemove,
        Action closeDialog)
    {
        var message = item.Status is DownloadStatus.Resolving or DownloadStatus.Downloading
            ? "This download is active. Removing it will cancel the current job."
            : "Remove this download from the queue?";
        var detail = item.Title;

        return BuildConfirmContent(message, detail, "Remove", onRemove, closeDialog);
    }

    private static Control BuildConfirmContent(
        string message,
        string detail,
        string confirmText,
        Action onConfirm,
        Action closeDialog)
    {
        var cancelButton = new Button
        {
            Content = "Cancel",
            HorizontalAlignment = HorizontalAlignment.Right
        };
        cancelButton.Classes.Add("secondary");
        cancelButton.Click += (_, _) => closeDialog();

        var removeButton = new Button
        {
            Content = confirmText,
            HorizontalAlignment = HorizontalAlignment.Right
        };
        removeButton.Classes.Add("primary");
        removeButton.Click += (_, _) => onConfirm();

        return new Border
        {
            Padding = new global::Avalonia.Thickness(18),
            Child = new StackPanel
            {
                Spacing = 16,
                Children =
                {
                    new TextBlock
                    {
                        Text = message,
                        TextWrapping = TextWrapping.Wrap
                    },
                    new TextBlock
                    {
                        Text = detail,
                        TextWrapping = TextWrapping.Wrap,
                        Foreground = Brushes.LightSlateGray
                    },
                    new StackPanel
                    {
                        Orientation = Orientation.Horizontal,
                        Spacing = 10,
                        HorizontalAlignment = HorizontalAlignment.Right,
                        Children = { cancelButton, removeButton }
                    }
                }
            }
        };
    }

    private static void RegisterConfirmKeys(Window dialog, Action onConfirm, Action onCancel)
    {
        dialog.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter)
            {
                onConfirm();
                e.Handled = true;
            }
            else if (e.Key == Key.Escape)
            {
                onCancel();
                e.Handled = true;
            }
        };
    }
}
