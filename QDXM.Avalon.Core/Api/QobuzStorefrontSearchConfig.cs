namespace QDXM.Avalon.Core.Api;

public sealed record QobuzStorefrontSearchConfig(
    string ApplicationId,
    string ApiKey,
    string LabelsIndex)
{
    public string LabelsEndpoint =>
        $"https://{ApplicationId}-dsn.algolia.net/1/indexes/{LabelsIndex}/query";
}
