using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core;
using Avalonia.Data.Core.Plugins;
using System.Linq;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using QDXM.Avalon.ViewModels;
using QDXM.Avalon.Views;
using QDXM.Avalon.Core.Downloads;
using QDXM.Avalon.Core.Protocol;
using QDXM.Avalon.Core.Settings;
using QDXM.Avalon.Core.Tools;
using QDXM.Avalon.Services;

namespace QDXM.Avalon;

public partial class App : Application
{
    private Mutex? singleInstanceMutex;
    private ProtocolUrlQueue? protocolUrlQueue;
    private bool ownsSingleInstanceMutex;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var protocolQueuePath = GetProtocolQueuePath();
            var importedUrls = GetImportedUrls(desktop.Args);
            var startupUrls = GetQueueableStartupUrls(desktop.Args)
                .Concat(importedUrls.Urls)
                .ToList();

            singleInstanceMutex = new Mutex(false, "QDXM_Avalon_SingleInstance");
            ownsSingleInstanceMutex = singleInstanceMutex.WaitOne(0);
            if (!ownsSingleInstanceMutex)
            {
                if (startupUrls.Count > 0 || !string.IsNullOrWhiteSpace(importedUrls.ErrorMessage))
                {
                    using var queue = new ProtocolUrlQueue(protocolQueuePath);
                    if (!string.IsNullOrWhiteSpace(importedUrls.ErrorMessage))
                    {
                        queue.AddWarningToQueue(importedUrls.ErrorMessage);
                    }

                    foreach (var url in startupUrls)
                    {
                        queue.AddToQueue(url);
                    }
                }

                Environment.Exit(0);
                return;
            }

            RemoteImageCache.ClearPersistentDiskCache();

            var settingsStore = new JsonSettingsStore();
            settingsStore.LoadAsync().GetAwaiter().GetResult();
            var viewModel = new MainWindowViewModel(settingsStore);
            if (!string.IsNullOrWhiteSpace(importedUrls.ErrorMessage))
            {
                viewModel.LogService.Warning("Import", importedUrls.ErrorMessage);
            }

            var mainWindow = new MainWindow
            {
                DataContext = viewModel,
            };
            desktop.MainWindow = mainWindow;

            Dispatcher.UIThread.Post(() =>
            {
                mainWindow.Show();
                mainWindow.Activate();
            });

            protocolUrlQueue = new ProtocolUrlQueue(protocolQueuePath);
            protocolUrlQueue.UrlReceived += url =>
                Dispatcher.UIThread.Post(() => viewModel.EnqueueExternalUrl(url));
            protocolUrlQueue.WarningReceived += message =>
                Dispatcher.UIThread.Post(() => viewModel.LogService.Warning("Import", message));
            protocolUrlQueue.Initialize();

            foreach (var url in startupUrls)
            {
                viewModel.EnqueueExternalUrl(url);
            }

            desktop.Exit += (_, _) =>
            {
                viewModel.PrepareForShutdownAsync().GetAwaiter().GetResult();
                protocolUrlQueue?.Dispose();
                if (ownsSingleInstanceMutex)
                {
                    singleInstanceMutex?.ReleaseMutex();
                }

                singleInstanceMutex?.Dispose();
            };
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static bool IsQueueableStartupUrl(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        return ProtocolHandler.IsProtocolUrl(value) ||
            value.Contains("qobuz.com", StringComparison.OrdinalIgnoreCase);
    }

    private static string GetProtocolQueuePath()
    {
        return AppDataPaths.ProtocolQueueDirectory;
    }

    private static IReadOnlyList<string> GetQueueableStartupUrls(IReadOnlyList<string>? args)
    {
        if (args is null || args.Count == 0)
        {
            return [];
        }

        var urls = new List<string>();
        for (var index = 0; index < args.Count; index++)
        {
            var arg = args[index];
            if (DownloadLinkImportFile.IsImportFlag(arg))
            {
                index++;
                continue;
            }

            if (DownloadLinkImportFile.IsInlineImportArgument(arg))
            {
                continue;
            }

            if (IsQueueableStartupUrl(arg))
            {
                urls.Add(arg);
            }
        }

        return urls;
    }

    private static ImportFileResult GetImportedUrls(IReadOnlyList<string>? args)
    {
        if (!DownloadLinkImportFile.TryGetImportFilePath(args, out var importFilePath, out var pathErrorMessage))
        {
            return new ImportFileResult([], pathErrorMessage);
        }

        if (string.IsNullOrWhiteSpace(importFilePath))
        {
            return new ImportFileResult([], "Import file path is empty.");
        }

        return DownloadLinkImportFile.TryReadLinks(importFilePath, out var links, out var errorMessage)
            ? new ImportFileResult(links, null)
            : new ImportFileResult([], errorMessage);
    }

    private sealed record ImportFileResult(IReadOnlyList<string> Urls, string? ErrorMessage);
}
