using QDXM.Avalon.Core.Downloads;
using QDXM.Avalon.Services;
using QDXM.Avalon.ViewModels;

namespace QDXM.Avalon.Tests;

public sealed class DownloadStatusTextTests
{
    [Theory]
    [InlineData(DownloadStatus.Queued, DownloadStatusText.Queued)]
    [InlineData(DownloadStatus.Resolving, DownloadStatusText.Resolving)]
    [InlineData(DownloadStatus.Downloading, DownloadStatusText.Downloading)]
    [InlineData(DownloadStatus.Paused, DownloadStatusText.Paused)]
    [InlineData(DownloadStatus.Completed, DownloadStatusText.Completed)]
    [InlineData(DownloadStatus.Failed, DownloadStatusText.Failed)]
    [InlineData(DownloadStatus.Canceled, DownloadStatusText.Canceled)]
    [InlineData(DownloadStatus.Skipped, DownloadStatusText.Skipped)]
    [InlineData(DownloadStatus.Issues, DownloadStatusText.Issues)]
    public void ForStatus_ReturnsSharedDisplayText(DownloadStatus status, string expectedText)
    {
        Assert.Equal(expectedText, DownloadStatusText.ForStatus(status));
    }

    [Fact]
    public void DownloadQueueItemViewModel_StatusDisplayUsesSharedText()
    {
        var item = new DownloadQueueItemViewModel
        {
            Status = DownloadStatus.Paused
        };

        Assert.Equal(DownloadStatusText.Paused, item.StatusDisplay);
    }
}
