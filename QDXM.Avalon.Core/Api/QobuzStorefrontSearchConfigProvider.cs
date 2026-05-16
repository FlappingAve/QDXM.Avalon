using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace QDXM.Avalon.Core.Api;

public sealed class QobuzStorefrontSearchConfigProvider
{
    private const string SearchPageUrl = "https://www.qobuz.com/us-en/search?q=";
    private static readonly Regex AlgoliaConfigPattern = new(
        @"window\.qobuz\.algolia2\s*=\s*(?<json>\{.*?\});",
        RegexOptions.Compiled | RegexOptions.Singleline);

    private readonly HttpClient httpClient;
    private readonly object configLock = new();
    private Task<QobuzStorefrontSearchConfig>? configTask;

    public QobuzStorefrontSearchConfigProvider()
        : this(new HttpClient())
    {
    }

    internal QobuzStorefrontSearchConfigProvider(HttpClient httpClient)
    {
        this.httpClient = httpClient;
    }

    public Task<QobuzStorefrontSearchConfig> GetConfigAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        TaskCompletionSource<QobuzStorefrontSearchConfig>? completionToStart = null;
        Task<QobuzStorefrontSearchConfig> task;

        lock (configLock)
        {
            if (configTask is null)
            {
                completionToStart = new TaskCompletionSource<QobuzStorefrontSearchConfig>(
                    TaskCreationOptions.RunContinuationsAsynchronously);
                configTask = completionToStart.Task;
            }

            task = configTask;
        }

        if (completionToStart is not null)
        {
            _ = FetchConfigAndResetOnFailureAsync(completionToStart);
        }

        return task;
    }

    private async Task FetchConfigAndResetOnFailureAsync(TaskCompletionSource<QobuzStorefrontSearchConfig> completion)
    {
        try
        {
            completion.SetResult(await FetchConfigAsync(httpClient).ConfigureAwait(false));
        }
        catch (Exception ex)
        {
            lock (configLock)
            {
                configTask = null;
            }

            completion.SetException(ex);
        }
    }

    private static async Task<QobuzStorefrontSearchConfig> FetchConfigAsync(HttpClient httpClient)
    {
        var html = await httpClient.GetStringAsync(SearchPageUrl).ConfigureAwait(false);
        return ParseConfig(html);
    }

    public static QobuzStorefrontSearchConfig ParseConfig(string html)
    {
        var match = AlgoliaConfigPattern.Match(html);
        if (!match.Success)
        {
            throw new InvalidOperationException("Qobuz storefront search config was not found.");
        }

        var parsed = JsonSerializer.Deserialize<StorefrontAlgoliaConfig>(match.Groups["json"].Value);
        if (parsed is null ||
            string.IsNullOrWhiteSpace(parsed.ApplicationId) ||
            string.IsNullOrWhiteSpace(parsed.ApiKey) ||
            string.IsNullOrWhiteSpace(parsed.Index?.MainLabels))
        {
            throw new InvalidOperationException("Qobuz storefront search config is incomplete.");
        }

        return new QobuzStorefrontSearchConfig(
            parsed.ApplicationId,
            parsed.ApiKey,
            parsed.Index.MainLabels);
    }

    private sealed class StorefrontAlgoliaConfig
    {
        [JsonPropertyName("application_id")]
        public string? ApplicationId { get; set; }

        [JsonPropertyName("api_key")]
        public string? ApiKey { get; set; }

        [JsonPropertyName("index")]
        public StorefrontAlgoliaIndexes? Index { get; set; }
    }

    private sealed class StorefrontAlgoliaIndexes
    {
        [JsonPropertyName("main_labels")]
        public string? MainLabels { get; set; }
    }
}
