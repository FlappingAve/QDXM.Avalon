using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using QobuzApiSharp.Models.User;
using QobuzApiSharp.Service;

namespace QDXM.Avalon.Core.Auth;

public sealed class QobuzBrowserLoginService
{
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromMinutes(5);

    public async Task<QobuzBrowserLoginResult> LoginAsync(
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
    {
        using var api = new QobuzApiService();
        if (string.IsNullOrWhiteSpace(api.OAuthPrivateKey))
        {
            throw new InvalidOperationException("Qobuz browser login is unavailable because the OAuth private key could not be found.");
        }

        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();

        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var redirectUrl = $"http://127.0.0.1:{port}/";
        var loginUrl =
            "https://www.qobuz.com/signin/oauth" +
            $"?ext_app_id={Uri.EscapeDataString(api.AppId)}" +
            $"&redirect_url={Uri.EscapeDataString(redirectUrl)}";

        OpenSystemBrowser(loginUrl);

        using var timeoutCts = new CancellationTokenSource(timeout ?? DefaultTimeout);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

        var code = await WaitForAuthorizationCodeAsync(listener, linkedCts.Token).ConfigureAwait(false);
        var login = await Task.Run(() => api.LoginWithOAuthCode(code), cancellationToken).ConfigureAwait(false);

        return new QobuzBrowserLoginResult(api.AppId, api.AppSecret, login);
    }

    private static void OpenSystemBrowser(string url)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = url,
            UseShellExecute = true
        });
    }

    private static async Task<string> WaitForAuthorizationCodeAsync(
        TcpListener listener,
        CancellationToken cancellationToken)
    {
        while (true)
        {
            using var client = await listener.AcceptTcpClientAsync(cancellationToken).ConfigureAwait(false);
            var stream = client.GetStream();
            var requestTarget = await ReadRequestTargetAsync(stream, cancellationToken).ConfigureAwait(false);
            var code = TryGetAuthorizationCode(requestTarget);

            await WriteCallbackResponseAsync(stream, code is not null, cancellationToken).ConfigureAwait(false);

            if (!string.IsNullOrWhiteSpace(code))
            {
                return code;
            }
        }
    }

    private static async Task<string> ReadRequestTargetAsync(
        NetworkStream stream,
        CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(stream, Encoding.ASCII, leaveOpen: true);
        var requestLine = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(requestLine))
        {
            return "/";
        }

        var parts = requestLine.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length >= 2 ? parts[1] : "/";
    }

    internal static string? TryGetAuthorizationCode(string requestTarget)
    {
        if (!Uri.TryCreate("http://127.0.0.1" + requestTarget, UriKind.Absolute, out var uri))
        {
            return null;
        }

        foreach (var pair in uri.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var pieces = pair.Split('=', 2);
            if (pieces.Length != 2)
            {
                continue;
            }

            var key = Uri.UnescapeDataString(pieces[0].Replace("+", " "));
            if (!string.Equals(key, "code_autorisation", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(key, "code", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            return Uri.UnescapeDataString(pieces[1].Replace("+", " "));
        }

        return null;
    }

    private static async Task WriteCallbackResponseAsync(
        NetworkStream stream,
        bool success,
        CancellationToken cancellationToken)
    {
        var title = success ? "Login successful" : "Login failed";
        var message = success
            ? "You can close this tab and return to QDXM Avalon."
            : "No authorization code was received. Please return to QDXM Avalon and try again.";
        var body =
            "<!doctype html><html><head><meta charset=\"utf-8\"><title>" + title + "</title></head>" +
            "<body style=\"font-family:Segoe UI,system-ui,sans-serif;text-align:center;padding:64px\">" +
            "<h2>" + title + "</h2><p>" + message + "</p></body></html>";
        var header =
            "HTTP/1.1 200 OK\r\n" +
            "Content-Type: text/html; charset=utf-8\r\n" +
            $"Content-Length: {Encoding.UTF8.GetByteCount(body)}\r\n" +
            "Connection: close\r\n\r\n";

        var bytes = Encoding.UTF8.GetBytes(header + body);
        await stream.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
    }
}

public sealed record QobuzBrowserLoginResult(string AppId, string AppSecret, Login Login);
