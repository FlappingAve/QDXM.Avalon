using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using QDXM.Avalon.ViewModels;

namespace QDXM.Avalon.Views;

public partial class MainWindow : Window
{
    private bool isShuttingDown;

    public MainWindow()
    {
        InitializeComponent();
        Closed += async (_, _) => await ShutdownApplicationAsync();
    }

    private async Task ShutdownApplicationAsync()
    {
        if (isShuttingDown)
        {
            return;
        }

        isShuttingDown = true;

        if (DataContext is MainWindowViewModel viewModel)
        {
            await viewModel.PrepareForShutdownAsync();
        }

        if (App.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.Shutdown(0);
        }

        Environment.Exit(0);
    }

    private void Window_OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        if (e.Key == Key.L && e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            viewModel.ShowDownloadsCommand.Execute(null);
            Dispatcher.UIThread.Post(FocusUrlTextBox);
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Delete &&
            viewModel.IsDownloadsSelected &&
            e.Source is not TextBox &&
            viewModel.Downloads.RemoveSelectedCommand.CanExecute(null))
        {
            viewModel.Downloads.RemoveSelectedCommand.Execute(null);
            e.Handled = true;
        }
    }

    private void FocusUrlTextBox()
    {
        var textBox = this
            .GetVisualDescendants()
            .OfType<TextBox>()
            .FirstOrDefault(textBox => textBox.Name == "UrlTextBox");

        textBox?.Focus();
        textBox?.SelectAll();
    }
}
