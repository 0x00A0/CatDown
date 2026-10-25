using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace CatDown.Core.Secrets;

// Secrets such as API keys, kept in the operating system's credential store.
public interface ICredentialStore
{
    bool IsAvailable { get; }
    string? Read(string name);
    void Write(string name, string secret);
    void Delete(string name);
}

public static class CredentialStores
{
    public const string Service = "CatDown";

    public static ICredentialStore ForCurrentPlatform() =>
        OperatingSystem.IsWindows() ? new WindowsCredentialStore()
        : OperatingSystem.IsMacOS() ? new MacCredentialStore()
        : new UnavailableCredentialStore();
}

public sealed class UnavailableCredentialStore : ICredentialStore
{
    public bool IsAvailable => false;
    public string? Read(string name) => null;
    public void Write(string name, string secret) => throw new InvalidOperationException("No credential store on this platform.");
    public void Delete(string name) { }
}

// Windows Credential Manager, generic credentials named "CatDown:<name>".
public sealed class WindowsCredentialStore : ICredentialStore
{
    const uint GenericType = 1;
    const uint PersistLocalMachine = 2;

    public bool IsAvailable => OperatingSystem.IsWindows();

    static string Target(string name) => $"{CredentialStores.Service}:{name}";

    public string? Read(string name)
    {
        if (!CredReadW(Target(name), GenericType, 0, out var pointer))
            return null;
        try
        {
            var credential = Marshal.PtrToStructure<Credential>(pointer);
            if (credential.CredentialBlobSize == 0)
                return "";
            var bytes = new byte[credential.CredentialBlobSize];
            Marshal.Copy(credential.CredentialBlob, bytes, 0, bytes.Length);
            return Encoding.UTF8.GetString(bytes);
        }
        finally
        {
            CredFree(pointer);
        }
    }

    public void Write(string name, string secret)
    {
        var bytes = Encoding.UTF8.GetBytes(secret);
        var blob = Marshal.AllocHGlobal(Math.Max(bytes.Length, 1));
        try
        {
            Marshal.Copy(bytes, 0, blob, bytes.Length);
            var credential = new Credential
            {
                Type = GenericType,
                TargetName = Target(name),
                CredentialBlobSize = (uint)bytes.Length,
                CredentialBlob = blob,
                Persist = PersistLocalMachine,
                UserName = Environment.UserName,
            };
            if (!CredWriteW(ref credential, 0))
                throw new InvalidOperationException($"CredWrite failed with error {Marshal.GetLastWin32Error()}.");
        }
        finally
        {
            Marshal.Copy(new byte[bytes.Length], 0, blob, bytes.Length);
            Marshal.FreeHGlobal(blob);
            System.Security.Cryptography.CryptographicOperations.ZeroMemory(bytes);
        }
    }

    public void Delete(string name) => CredDeleteW(Target(name), GenericType, 0);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    struct Credential
    {
        public uint Flags;
        public uint Type;
        public string TargetName;
        public string? Comment;
        public System.Runtime.InteropServices.ComTypes.FILETIME LastWritten;
        public uint CredentialBlobSize;
        public IntPtr CredentialBlob;
        public uint Persist;
        public uint AttributeCount;
        public IntPtr Attributes;
        public string? TargetAlias;
        public string? UserName;
    }

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    static extern bool CredReadW(string target, uint type, uint flags, out IntPtr credential);

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    static extern bool CredWriteW(ref Credential credential, uint flags);

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    static extern bool CredDeleteW(string target, uint type, uint flags);

    [DllImport("advapi32.dll")]
    static extern void CredFree(IntPtr buffer);
}

// macOS keychain through the `security` command, service "CatDown", account <name>.
// `security -w` prints a password with non-ASCII characters as hex, so secrets are stored
// as "b64:" + Base64 to keep them ASCII and readable back exactly.
public sealed class MacCredentialStore : ICredentialStore
{
    const string Base64Prefix = "b64:";

    public bool IsAvailable => OperatingSystem.IsMacOS();

    public string? Read(string name)
    {
        var (code, output) = Run("find-generic-password", "-s", CredentialStores.Service, "-a", name, "-w");
        return code == 0 ? Decode(output.TrimEnd('\n')) : null;
    }

    public static string Encode(string secret) => Base64Prefix + Convert.ToBase64String(Encoding.UTF8.GetBytes(secret));

    // Values stored some other way (no prefix) come back as they are.
    public static string Decode(string stored)
    {
        if (!stored.StartsWith(Base64Prefix, StringComparison.Ordinal))
            return stored;
        try
        {
            return Encoding.UTF8.GetString(Convert.FromBase64String(stored[Base64Prefix.Length..]));
        }
        catch (FormatException)
        {
            return stored;
        }
    }

    public void Write(string name, string secret)
    {
        var (code, _) = Run("add-generic-password", "-U", "-s", CredentialStores.Service, "-a", name, "-w", Encode(secret));
        if (code != 0)
            throw new InvalidOperationException($"security add-generic-password failed with exit code {code}.");
    }

    public void Delete(string name) => Run("delete-generic-password", "-s", CredentialStores.Service, "-a", name);

    static (int Code, string Output) Run(params string[] arguments)
    {
        var start = new ProcessStartInfo("/usr/bin/security") { RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var argument in arguments)
            start.ArgumentList.Add(argument);
        using var process = Process.Start(start)!;
        var output = process.StandardOutput.ReadToEnd();
        process.WaitForExit();
        return (process.ExitCode, output);
    }
}
