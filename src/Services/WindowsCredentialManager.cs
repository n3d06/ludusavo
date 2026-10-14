using System;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;

namespace ludusavo.Services;

public class WindowsCredentialManager : ICredentialManager
{
    private const string TargetName = "sav:GoogleDrive";
    private const int CRED_TYPE_GENERIC = 1;
    private const int CRED_PERSIST_LOCAL_MACHINE = 2;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct CREDENTIAL
    {
        public int Flags;
        public int Type;
        public string TargetName;
        public string Comment;
        public long LastWritten;
        public int CredentialBlobSize;
        public IntPtr CredentialBlob;
        public int Persist;
        public int AttributeCount;
        public IntPtr Attributes;
        public string TargetAlias;
        public string UserName;
    }

    [DllImport("Advapi32.dll", EntryPoint = "CredWriteW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CredWrite([In] ref CREDENTIAL userCredential, [In] int flags);

    [DllImport("Advapi32.dll", EntryPoint = "CredReadW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CredRead(string target, int type, int reservedFlag, out IntPtr credentialPtr);

    [DllImport("Advapi32.dll", EntryPoint = "CredDeleteW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CredDelete(string target, int type, int flags);

    [DllImport("Advapi32.dll", EntryPoint = "CredFree", SetLastError = true)]
    private static extern void CredFree([In] IntPtr cred);

    private class CredentialPayload
    {
        public string? RefreshToken { get; set; }
        public string? AccessToken { get; set; }
        public DateTime? Expiry { get; set; }
        public string? ClientSecret { get; set; }
        public string? UserEmail { get; set; }
    }

    public bool SaveGoogleCredentials(string? refreshToken, string? accessToken, DateTime? expiry, string? clientSecret, string? userEmail)
    {
        try
        {
            var payload = new CredentialPayload
            {
                RefreshToken = refreshToken,
                AccessToken = accessToken,
                Expiry = expiry,
                ClientSecret = clientSecret,
                UserEmail = userEmail
            };

            var json = JsonSerializer.Serialize(payload);
            var bytes = Encoding.UTF8.GetBytes(json);
            var handle = Marshal.AllocHGlobal(bytes.Length);
            try
            {
                Marshal.Copy(bytes, 0, handle, bytes.Length);

                var cred = new CREDENTIAL
                {
                    Type = CRED_TYPE_GENERIC,
                    TargetName = TargetName,
                    Comment = "sav CLI Google Drive OAuth Credentials",
                    CredentialBlobSize = bytes.Length,
                    CredentialBlob = handle,
                    Persist = CRED_PERSIST_LOCAL_MACHINE,
                    UserName = string.IsNullOrWhiteSpace(userEmail) ? "GoogleDrive" : userEmail
                };

                return CredWrite(ref cred, 0);
            }
            finally
            {
                Marshal.FreeHGlobal(handle);
            }
        }
        catch
        {
            return false;
        }
    }

    public (string? refreshToken, string? accessToken, DateTime? expiry, string? clientSecret, string? userEmail) LoadGoogleCredentials()
    {
        try
        {
            if (CredRead(TargetName, CRED_TYPE_GENERIC, 0, out var credPtr))
            {
                try
                {
                    var cred = Marshal.PtrToStructure<CREDENTIAL>(credPtr);
                    if (cred.CredentialBlobSize > 0 && cred.CredentialBlob != IntPtr.Zero)
                    {
                        var bytes = new byte[cred.CredentialBlobSize];
                        Marshal.Copy(cred.CredentialBlob, bytes, 0, cred.CredentialBlobSize);
                        var json = Encoding.UTF8.GetString(bytes);
                        var payload = JsonSerializer.Deserialize<CredentialPayload>(json);
                        if (payload != null)
                        {
                            return (payload.RefreshToken, payload.AccessToken, payload.Expiry, payload.ClientSecret, payload.UserEmail ?? cred.UserName);
                        }
                    }
                }
                finally
                {
                    CredFree(credPtr);
                }
            }
        }
        catch
        {
        }

        return (null, null, null, null, null);
    }

    public bool DeleteGoogleCredentials()
    {
        try
        {
            return CredDelete(TargetName, CRED_TYPE_GENERIC, 0);
        }
        catch
        {
            return false;
        }
    }
}
