using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Serilog;

namespace SpaceWay.Core.Accounts;

/// <summary>
/// Storage for sign-in tokens.
/// </summary>
public interface ITokenStore
{
    /// <summary>The storage is protected by the OS rather than kept in plain text.</summary>
    bool IsSecure { get; }

    AuthToken? Get(string key);

    void Set(string key, AuthToken token);

    void Remove(string key);
}

public static class TokenStoreFactory
{
    /// <summary>
    /// Picks the storage for the current OS, falling back to a file
    /// if the keystore is unavailable.
    /// </summary>
    public static ITokenStore Create(string fallbackDirectory)
    {
        if (OperatingSystem.IsWindows())
            return new DpapiTokenStore(Path.Combine(fallbackDirectory, "tokens.dat"));

        if (OperatingSystem.IsLinux() && CommandLineTokenStore.IsSecretToolAvailable())
            return CommandLineTokenStore.SecretTool();

        if (OperatingSystem.IsMacOS())
            return CommandLineTokenStore.Keychain();

        Log.Warning(
            "System keystore unavailable, tokens will be stored " +
            "in plain text at: {Path}", fallbackDirectory);

        return new PlainFileTokenStore(Path.Combine(fallbackDirectory, "tokens.json"));
    }
}

/// <summary>
/// Windows: DPAPI encryption with the current user's key.
/// The file cannot be decrypted under a different user account.
/// </summary>
[System.Runtime.Versioning.SupportedOSPlatform("windows")]
internal sealed class DpapiTokenStore(string path) : ITokenStore
{
    private readonly PlainFileTokenStore _inner = new(path, Protect, Unprotect);

    public bool IsSecure => true;

    public AuthToken? Get(string key) => _inner.Get(key);

    public void Set(string key, AuthToken token) => _inner.Set(key, token);

    public void Remove(string key) => _inner.Remove(key);

    private static byte[] Protect(byte[] data) =>
        ProtectedData.Protect(data, null, DataProtectionScope.CurrentUser);

    private static byte[] Unprotect(byte[] data) =>
        ProtectedData.Unprotect(data, null, DataProtectionScope.CurrentUser);
}

/// <summary>
/// Linux and macOS: the system keystore's standard utilities.
/// </summary>
internal sealed class CommandLineTokenStore : ITokenStore
{
    private const string ServiceName = "SpaceWayLauncher";

    private readonly Func<string, string?> _read;
    private readonly Action<string, string> _write;
    private readonly Action<string> _delete;

    private CommandLineTokenStore(
        Func<string, string?> read,
        Action<string, string> write,
        Action<string> delete)
    {
        _read = read;
        _write = write;
        _delete = delete;
    }

    public bool IsSecure => true;

    public static bool IsSecretToolAvailable()
    {
        try
        {
            return Run("secret-tool", ["--version"], out _);
        }
        catch
        {
            return false;
        }
    }

    public static CommandLineTokenStore SecretTool() => new(
        read: key => Run("secret-tool", ["lookup", "service", ServiceName, "account", key], out var value)
            ? value
            : null,
        write: (key, value) => RunWithInput(
            "secret-tool",
            ["store", "--label", $"{ServiceName} ({key})", "service", ServiceName, "account", key],
            value),
        delete: key => Run("secret-tool", ["clear", "service", ServiceName, "account", key], out _));

    public static CommandLineTokenStore Keychain() => new(
        read: key => Run(
            "security",
            ["find-generic-password", "-s", ServiceName, "-a", key, "-w"],
            out var value)
            ? value
            : null,
        write: (key, value) => Run(
            "security",
            ["add-generic-password", "-U", "-s", ServiceName, "-a", key, "-w", value],
            out _),
        delete: key => Run("security", ["delete-generic-password", "-s", ServiceName, "-a", key], out _));

    public AuthToken? Get(string key)
    {
        try
        {
            var raw = _read(key);
            return string.IsNullOrWhiteSpace(raw)
                ? null
                : JsonSerializer.Deserialize<AuthToken>(raw);
        }
        catch (Exception e)
        {
            Log.Warning(e, "Failed to read token from keystore");
            return null;
        }
    }

    public void Set(string key, AuthToken token)
    {
        try
        {
            _write(key, JsonSerializer.Serialize(token));
        }
        catch (Exception e)
        {
            Log.Error(e, "Failed to save token to keystore");
        }
    }

    public void Remove(string key)
    {
        try
        {
            _delete(key);
        }
        catch (Exception e)
        {
            Log.Warning(e, "Failed to delete token from keystore");
        }
    }

    private static bool Run(string executable, string[] args, out string output)
    {
        using var process = Process.Start(new ProcessStartInfo(executable)
        {
            Arguments = string.Join(' ', args.Select(Quote)),
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        });

        if (process == null)
        {
            output = string.Empty;
            return false;
        }

        output = process.StandardOutput.ReadToEnd().Trim();
        process.WaitForExit();
        return process.ExitCode == 0;
    }

    private static void RunWithInput(string executable, string[] args, string input)
    {
        using var process = Process.Start(new ProcessStartInfo(executable)
        {
            Arguments = string.Join(' ', args.Select(Quote)),
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
        });

        if (process == null)
            return;

        process.StandardInput.Write(input);
        process.StandardInput.Close();
        process.WaitForExit();
    }

    private static string Quote(string value) => $"\"{value.Replace("\"", "\\\"")}\"";
}

/// <summary>
/// File on disk. Contents are encrypted if a protector is given;
/// otherwise stored as plain text, as a fallback for systems without a keystore.
/// </summary>
internal sealed class PlainFileTokenStore : ITokenStore
{
    private readonly string _path;
    private readonly Func<byte[], byte[]>? _protect;
    private readonly Func<byte[], byte[]>? _unprotect;

    public PlainFileTokenStore(
        string path,
        Func<byte[], byte[]>? protect = null,
        Func<byte[], byte[]>? unprotect = null)
    {
        _path = path;
        _protect = protect;
        _unprotect = unprotect;
    }

    public bool IsSecure => _protect != null;

    public AuthToken? Get(string key) => Load().GetValueOrDefault(key);

    public void Set(string key, AuthToken token)
    {
        var all = Load();
        all[key] = token;
        Save(all);
    }

    public void Remove(string key)
    {
        var all = Load();
        if (all.Remove(key))
            Save(all);
    }

    private Dictionary<string, AuthToken> Load()
    {
        if (!File.Exists(_path))
            return [];

        try
        {
            var bytes = File.ReadAllBytes(_path);
            if (_unprotect != null)
                bytes = _unprotect(bytes);

            return JsonSerializer.Deserialize<Dictionary<string, AuthToken>>(
                Encoding.UTF8.GetString(bytes)) ?? [];
        }
        catch (Exception e)
        {
            Log.Warning(e, "Failed to read token storage, starting empty");
            return [];
        }
    }

    private void Save(Dictionary<string, AuthToken> tokens)
    {
        try
        {
            if (Path.GetDirectoryName(_path) is { Length: > 0 } dir)
                Directory.CreateDirectory(dir);

            var bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(tokens));
            if (_protect != null)
                bytes = _protect(bytes);

            File.WriteAllBytes(_path, bytes);
        }
        catch (Exception e)
        {
            Log.Error(e, "Failed to save token storage");
        }
    }
}
