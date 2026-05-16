using QobuzApiSharp.Service;

namespace QDXM.Avalon.Core.Api;

public sealed class QobuzApiServiceFactory
{
    private static readonly object DynamicCredentialLock = new();
    private static DynamicQobuzApiCredentials? dynamicCredentials;

    private readonly string? appId;
    private readonly string? appSecret;
    private readonly string? userAuthToken;

    public QobuzApiServiceFactory(
        string? appId = null,
        string? appSecret = null,
        string? userAuthToken = null)
    {
        this.appId = FirstNonEmpty(appId, Environment.GetEnvironmentVariable("QDXM_AVALON_APP_ID"));
        this.appSecret = FirstNonEmpty(appSecret, Environment.GetEnvironmentVariable("QDXM_AVALON_APP_SECRET"));
        this.userAuthToken = FirstNonEmpty(userAuthToken, Environment.GetEnvironmentVariable("QDXM_AVALON_USER_AUTH_TOKEN"));
    }

    public bool HasUserAuthToken => userAuthToken is not null;

    public QobuzApiService Create()
    {
        var credentials = appId is not null && appSecret is not null
            ? new DynamicQobuzApiCredentials(appId, appSecret)
            : ResolveDynamicCredentials();

        var service = new QobuzApiService(credentials.AppId, credentials.AppSecret);

        if (userAuthToken is not null)
        {
            service.UserAuthToken = userAuthToken;
        }

        return service;
    }

    public static DynamicQobuzApiCredentials ResolveDynamicCredentials()
    {
        if (dynamicCredentials is not null)
        {
            return dynamicCredentials;
        }

        lock (DynamicCredentialLock)
        {
            if (dynamicCredentials is not null)
            {
                return dynamicCredentials;
            }

            using var service = new QobuzApiService();
            dynamicCredentials = new DynamicQobuzApiCredentials(service.AppId, service.AppSecret);
            return dynamicCredentials;
        }
    }

    public static void PrimeDynamicCredentials()
    {
        ResolveDynamicCredentials();
    }

    private static string? FirstNonEmpty(params string?[] values)
    {
        return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
    }
}

public sealed record DynamicQobuzApiCredentials(string AppId, string AppSecret);
