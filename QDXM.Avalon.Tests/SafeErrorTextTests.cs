using QDXM.Avalon.Services;
using QobuzApiSharp.Exceptions;
using QobuzApiSharp.Models;

namespace QDXM.Avalon.Tests;

public sealed class SafeErrorTextTests
{
    [Fact]
    public void FormatDownloadApiFailure_ExplainsMissingQobuzItem()
    {
        var exception = CreateApiException("404", "No result matching given argument");

        var message = SafeErrorText.FormatDownloadApiFailure(exception);

        Assert.Equal(
            "Download failed (404): May have been removed or no longer available in your region.",
            message);
    }

    [Fact]
    public void FormatDownloadApiFailure_KeepsGenericApiMessageForOtherFailures()
    {
        var exception = CreateApiException("500", "Service temporarily unavailable");

        var message = SafeErrorText.FormatDownloadApiFailure(exception);

        Assert.Equal("Download failed (500): Service temporarily unavailable", message);
    }

    private static ApiErrorResponseException CreateApiException(string code, string message)
    {
        return new ApiErrorResponseException(
            "API request failed.",
            "GET /track/get",
            new QobuzApiStatusResponse
            {
                Code = code,
                Message = message,
                Status = "error"
            });
    }
}
