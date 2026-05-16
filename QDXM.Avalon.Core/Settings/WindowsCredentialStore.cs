using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
using System.Text.Json;
using QDXM.Avalon.Core.Tools;

namespace QDXM.Avalon.Core.Settings;

public sealed class WindowsCredentialStore : IUserCredentialStore
{
    private const string TargetName = AppDataPaths.CredentialTargetName;
    private const int CredTypeGeneric = 1;
    private const int CredPersistLocalMachine = 2;

    public Task<UserCredential?> ReadAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!OperatingSystem.IsWindows())
        {
            return Task.FromResult<UserCredential?>(null);
        }

        if (!CredRead(TargetName, CredTypeGeneric, 0, out var credentialPointer))
        {
            var error = Marshal.GetLastWin32Error();
            if (error == 1168)
            {
                return Task.FromResult<UserCredential?>(null);
            }

            throw new Win32Exception(error);
        }

        try
        {
            var credential = Marshal.PtrToStructure<NativeCredential>(credentialPointer);
            var userId = Marshal.PtrToStringUni(credential.UserName) ?? string.Empty;
            var credentialPayload = string.Empty;

            if (credential.CredentialBlobSize > 0 && credential.CredentialBlob != IntPtr.Zero)
            {
                var bytes = new byte[credential.CredentialBlobSize];
                Marshal.Copy(credential.CredentialBlob, bytes, 0, bytes.Length);
                credentialPayload = Encoding.UTF8.GetString(bytes);
            }

            var payload = ParseCredentialPayload(credentialPayload);

            return Task.FromResult<UserCredential?>(new UserCredential(
                userId,
                payload.UserAuthToken ?? string.Empty,
                payload.AppId ?? string.Empty,
                payload.AppSecret ?? string.Empty));
        }
        finally
        {
            CredFree(credentialPointer);
        }
    }

    public Task SaveAsync(UserCredential credential, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!OperatingSystem.IsWindows())
        {
            return Task.CompletedTask;
        }

        if (string.IsNullOrWhiteSpace(credential.UserId) &&
            string.IsNullOrWhiteSpace(credential.UserAuthToken) &&
            string.IsNullOrWhiteSpace(credential.AppId) &&
            string.IsNullOrWhiteSpace(credential.AppSecret))
        {
            return DeleteAsync(cancellationToken);
        }

        var payload = JsonSerializer.Serialize(new CredentialPayload
        {
            UserAuthToken = credential.UserAuthToken,
            AppId = credential.AppId,
            AppSecret = credential.AppSecret
        });
        var payloadBytes = Encoding.UTF8.GetBytes(payload);
        var payloadPointer = IntPtr.Zero;
        var userNamePointer = IntPtr.Zero;

        try
        {
            payloadPointer = Marshal.AllocHGlobal(payloadBytes.Length);
            if (payloadBytes.Length > 0)
            {
                Marshal.Copy(payloadBytes, 0, payloadPointer, payloadBytes.Length);
            }

            userNamePointer = Marshal.StringToCoTaskMemUni(credential.UserId ?? string.Empty);

            var nativeCredential = new NativeCredential
            {
                AttributeCount = 0,
                Attributes = IntPtr.Zero,
                Comment = IntPtr.Zero,
                CredentialBlob = payloadPointer,
                CredentialBlobSize = payloadBytes.Length,
                Flags = 0,
                LastWritten = default,
                Persist = CredPersistLocalMachine,
                TargetAlias = IntPtr.Zero,
                TargetName = TargetName,
                Type = CredTypeGeneric,
                UserName = userNamePointer
            };

            if (!CredWrite(ref nativeCredential, 0))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }
        }
        finally
        {
            if (payloadPointer != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(payloadPointer);
            }

            if (userNamePointer != IntPtr.Zero)
            {
                Marshal.FreeCoTaskMem(userNamePointer);
            }
        }

        return Task.CompletedTask;
    }

    public Task DeleteAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!OperatingSystem.IsWindows())
        {
            return Task.CompletedTask;
        }

        if (!CredDelete(TargetName, CredTypeGeneric, 0))
        {
            var error = Marshal.GetLastWin32Error();
            if (error != 1168)
            {
                throw new Win32Exception(error);
            }
        }

        return Task.CompletedTask;
    }

    [DllImport("Advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool CredRead(
        string target,
        int type,
        int reservedFlag,
        out IntPtr credential);

    [DllImport("Advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool CredWrite(
        ref NativeCredential userCredential,
        int flags);

    [DllImport("Advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool CredDelete(
        string target,
        int type,
        int flags);

    [DllImport("Advapi32.dll", SetLastError = false)]
    private static extern void CredFree(IntPtr buffer);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NativeCredential
    {
        public int Flags;
        public int Type;
        [MarshalAs(UnmanagedType.LPWStr)]
        public string TargetName;
        public IntPtr Comment;
        public FILETIME LastWritten;
        public int CredentialBlobSize;
        public IntPtr CredentialBlob;
        public int Persist;
        public int AttributeCount;
        public IntPtr Attributes;
        public IntPtr TargetAlias;
        public IntPtr UserName;
    }

    private sealed class CredentialPayload
    {
        public string? UserAuthToken { get; set; }
        public string? AppId { get; set; }
        public string? AppSecret { get; set; }
    }

    private static CredentialPayload ParseCredentialPayload(string credentialPayload)
    {
        if (string.IsNullOrWhiteSpace(credentialPayload))
        {
            return new CredentialPayload();
        }

        try
        {
            return JsonSerializer.Deserialize<CredentialPayload>(credentialPayload) ?? new CredentialPayload();
        }
        catch (JsonException)
        {
            return new CredentialPayload
            {
                UserAuthToken = credentialPayload
            };
        }
    }
}
