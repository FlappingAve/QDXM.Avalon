using QobuzApiSharp.Exceptions;

namespace QDXM.Avalon.Services;

public static class SafeErrorText
{
    private const string QobuzNoResultMessage = "No result matching given argument";

    public static string FormatApiFailure(string action, ApiErrorResponseException exception)
    {
        var code = string.IsNullOrWhiteSpace(exception.ResponseStatusCode)
            ? "unknown"
            : exception.ResponseStatusCode;
        var reason = FirstNonEmpty(exception.ResponseReason, exception.ResponseStatus, "Qobuz rejected the request.");
        return $"{action} failed ({code}): {reason}";
    }

    public static string FormatDownloadApiFailure(ApiErrorResponseException exception)
    {
        if (IsMissingQobuzItem(exception))
        {
            return "Download failed (404): May have been removed or no longer available in your region.";
        }

        return FormatApiFailure("Download", exception);
    }

    public static string FormatUnexpectedFailure(string action)
    {
        return $"{action} failed. See Logs for details.";
    }

    public static string FormatUnexpectedLogMessage(Exception exception)
    {
        return $"{exception.GetType().Name}: {exception.Message}";
    }

    private static bool IsMissingQobuzItem(ApiErrorResponseException exception)
    {
        return string.Equals(exception.ResponseStatusCode, "404", StringComparison.OrdinalIgnoreCase)
            && ContainsQobuzNoResultText(exception.ResponseReason, exception.ResponseStatus, exception.Message);
    }

    private static bool ContainsQobuzNoResultText(params string?[] values)
    {
        return values.Any(value => value?.Contains(QobuzNoResultMessage, StringComparison.OrdinalIgnoreCase) == true);
    }

    private static string FirstNonEmpty(params string?[] values)
    {
        return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
    }
}
