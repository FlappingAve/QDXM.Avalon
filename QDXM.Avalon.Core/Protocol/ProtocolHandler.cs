using Microsoft.Win32;
using System.Diagnostics;
using QDXM.Avalon.Core.Api;

namespace QDXM.Avalon.Core.Protocol;

public static class ProtocolHandler
{
    public const string ProtocolName = "QDXMA";
    private const string ProtocolDescription = "QDXM Avalon Protocol";

    public static bool IsProtocolUrl(string? url)
    {
        return url?.StartsWith($"{ProtocolName}://", StringComparison.OrdinalIgnoreCase) == true;
    }

    public static string ConvertProtocolUrl(string protocolUrl)
    {
        if (string.IsNullOrWhiteSpace(protocolUrl))
        {
            return string.Empty;
        }

        var path = protocolUrl.Trim().Replace($"{ProtocolName}://", string.Empty, StringComparison.OrdinalIgnoreCase);
        return QobuzUrlBuilder.CreateOpenUrlFromPath(path);
    }

    public static void RegisterProtocol(string? executablePath = null)
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        try
        {
            var resolvedPath = executablePath
                ?? Environment.ProcessPath
                ?? Process.GetCurrentProcess().MainModule?.FileName;

            if (string.IsNullOrWhiteSpace(resolvedPath))
            {
                return;
            }

            using var key = Registry.CurrentUser.CreateSubKey($@"Software\Classes\{ProtocolName}");
            key?.SetValue(string.Empty, $"URL:{ProtocolDescription}");
            key?.SetValue("URL Protocol", string.Empty);

            using var iconKey = key?.CreateSubKey("DefaultIcon");
            iconKey?.SetValue(string.Empty, $"\"{resolvedPath}\",0");

            using var commandKey = key?.CreateSubKey(@"shell\open\command");
            commandKey?.SetValue(string.Empty, $"\"{resolvedPath}\" \"%1\"");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to register protocol handler: {ex.Message}");
        }
    }

    public static void UnregisterProtocol()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        try
        {
            Registry.CurrentUser.DeleteSubKeyTree($@"Software\Classes\{ProtocolName}", throwOnMissingSubKey: false);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to unregister protocol handler: {ex.Message}");
        }
    }

    public static bool IsProtocolRegistered()
    {
        if (!OperatingSystem.IsWindows())
        {
            return false;
        }

        using var key = Registry.CurrentUser.OpenSubKey($@"Software\Classes\{ProtocolName}");
        return key is not null;
    }
}
