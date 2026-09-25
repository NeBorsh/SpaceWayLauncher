using System.IO.MemoryMappedFiles;
using System.Reflection;
using System.Security.Cryptography;
using NSec.Cryptography;
using Serilog;

namespace SpaceWay.Core.Engine;

/// <summary>
/// Authenticity check for downloaded engine builds.
/// </summary>
public sealed class EngineSignature(PublicKey key)
{
    private const string EmbeddedKeyResource = "SpaceWay.Core.Engine.robust_signing_key.pub";

    /// <summary>Verifier using the Space Wizards key that signs Robust builds.</summary>
    public static EngineSignature Robust { get; } = new(LoadRobustKey());

    /// <summary>
    /// Verifies the Ed25519 signature of an entire file.
    /// </summary>
    /// <param name="path">Path to the file.</param>
    /// <param name="signature">Signature from the manifest, in hex.</param>
    public bool Verify(string path, string signature)
    {
        byte[] signatureBytes;
        try
        {
            signatureBytes = Convert.FromHexString(signature);
        }
        catch (FormatException)
        {
            Log.Warning("Manifest signature is not a hex string");
            return false;
        }

        using var file = File.OpenRead(path);

        if (file.Length == 0)
            return false;

        if (file.Length > int.MaxValue)
            throw new EngineUpdateException("File exceeds 2 GiB, cannot verify signature");

        using var mapping = MemoryMappedFile.CreateFromFile(
            file, null, 0, MemoryMappedFileAccess.Read, HandleInheritability.None, leaveOpen: true);
        using var view = mapping.CreateViewAccessor(0, file.Length, MemoryMappedFileAccess.Read);

        unsafe
        {
            byte* pointer = null;
            view.SafeMemoryMappedViewHandle.AcquirePointer(ref pointer);
            try
            {
                var data = new ReadOnlySpan<byte>(pointer, (int)file.Length);
                return SignatureAlgorithm.Ed25519.Verify(key, data, signatureBytes);
            }
            finally
            {
                view.SafeMemoryMappedViewHandle.ReleasePointer();
            }
        }
    }

    /// <summary>Checks a file's SHA-256 against the expected value, streaming.</summary>
    public static async Task<bool> VerifyHash(string path, string expectedHex, CancellationToken cancel = default)
    {
        byte[] expected;
        try
        {
            expected = Convert.FromHexString(expectedHex);
        }
        catch (FormatException)
        {
            Log.Warning("Manifest checksum is not a hex string");
            return false;
        }

        await using var file = File.OpenRead(path);
        var actual = await SHA256.HashDataAsync(file, cancel);

        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }

    private static PublicKey LoadRobustKey()
    {
        var overridePath = LauncherPaths.PathEnginePublicKey;
        if (File.Exists(overridePath))
        {
            Log.Warning("Using custom engine signing key: {Path}", overridePath);
            return Import(File.ReadAllBytes(overridePath));
        }

        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(EmbeddedKeyResource)
                           ?? throw new InvalidOperationException($"Missing resource{EmbeddedKeyResource}");
        using var memory = new MemoryStream();
        stream.CopyTo(memory);

        return Import(memory.ToArray());
    }

    private static PublicKey Import(byte[] blob) =>
        PublicKey.Import(SignatureAlgorithm.Ed25519, blob, KeyBlobFormat.PkixPublicKeyText);
}
