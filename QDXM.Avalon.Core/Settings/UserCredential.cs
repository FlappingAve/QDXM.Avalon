namespace QDXM.Avalon.Core.Settings;

public sealed record UserCredential(
    string UserId,
    string UserAuthToken,
    string AppId = "",
    string AppSecret = "");
