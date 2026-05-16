namespace QDXM.Avalon.Core.Settings;

public interface IUserCredentialStore
{
    Task<UserCredential?> ReadAsync(CancellationToken cancellationToken = default);

    Task SaveAsync(UserCredential credential, CancellationToken cancellationToken = default);

    Task DeleteAsync(CancellationToken cancellationToken = default);
}
