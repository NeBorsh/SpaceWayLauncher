using System.IO.Compression;
using NSec.Cryptography;
using Robust.LoaderApi;

namespace SpaceWay.Loader;

/// <summary>
/// Runs the game engine in its own process.
/// </summary>
internal static class Program
{
    private const string KeyResource = "SpaceWay.Loader.robust_signing_key.pub";

    [STAThread]
    internal static int Main(string[] args)
    {
        UseUtf8Output();

        if (args.Length < 2)
        {
            Console.Error.WriteLine(
                "Usage: SpaceWay.Loader <engine path> <signature> [engine arguments...]");
            return 1;
        }

        var enginePath = args[0];

        if (!VerifyEngine(enginePath, args[1]))
            return 2;

        return Run(enginePath, args[2..]) ? 0 : 3;
    }

    private static bool Run(string enginePath, string[] engineArgs)
    {
        using var engine = new EngineAssemblies(enginePath);

        if (!engine.TryLoad(EngineAssemblies.RobustAssemblyName, out var client))
        {
            Console.Error.WriteLine($"Engine build has no{EngineAssemblies.RobustAssemblyName}.dll");
            return false;
        }

        if (!engine.TryGetEntryPoint(client, out var entryPoint))
            return false;

        SQLitePCL.Batteries_V2.Init();

        ContentFileApi? content = null;
        ZipFileApi? overlay = null;
        ZipFileApi? bundle = null;

        try
        {
            var mounts = new List<ApiMount>();

            if (Environment.GetEnvironmentVariable("SPACEWAY_OVERLAY_ZIP") is { Length: > 0 } overlayPath)
            {
                overlay = new ZipFileApi(
                    new ZipArchive(File.OpenRead(overlayPath), ZipArchiveMode.Read));
                mounts.Add(new ApiMount(overlay, "/"));
            }

            if (Environment.GetEnvironmentVariable("SPACEWAY_BUNDLE_ZIP") is { Length: > 0 } bundlePath)
            {
                bundle = new ZipFileApi(
                    new ZipArchive(File.OpenRead(bundlePath), ZipArchiveMode.Read));
                mounts.Add(new ApiMount(bundle, "/"));
            }

            if (Environment.GetEnvironmentVariable("SPACEWAY_CONTENT_DB") is { Length: > 0 } databasePath
                && Environment.GetEnvironmentVariable("SPACEWAY_CONTENT_VERSION") is { Length: > 0 } version)
            {
                content = new ContentFileApi(databasePath, long.Parse(version));
                mounts.Add(new ApiMount(content, "/"));
            }

            var redial = Environment.GetEnvironmentVariable("SPACEWAY_LAUNCHER_PATH") is { Length: > 0 } launcher
                ? new RedialApi(launcher)
                : null;

            entryPoint.Main(new MainArgs(engineArgs, engine.Files, redial, mounts));
        }
        finally
        {
            content?.Dispose();
            overlay?.Dispose();
            bundle?.Dispose();
        }

        return true;
    }

    /// <summary>
    /// Verifies the engine build signature before loading anything from it.
    /// </summary>
    private static bool VerifyEngine(string enginePath, string signatureHex)
    {
        byte[] signature;
        try
        {
            signature = Convert.FromHexString(signatureHex);
        }
        catch (FormatException)
        {
            Console.Error.WriteLine("Engine signature is not a hex string");
            return false;
        }

        var engine = File.ReadAllBytes(enginePath);

        if (SignatureAlgorithm.Ed25519.Verify(LoadPublicKey(), engine, signature))
            return true;

#if DEBUG
        if (Environment.GetEnvironmentVariable("SPACEWAY_DISABLE_SIGNING") == "true")
        {
            Console.Error.WriteLine("Engine signature mismatch, but verification is disabled");
            return true;
        }
#endif

        Console.Error.WriteLine("Engine signature mismatch");
        return false;
    }

    /// <summary>
    /// Switches console output to UTF-8.
    /// </summary>
    private static void UseUtf8Output()
    {
        try
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;
        }
        catch (IOException)
        {
        }
    }

    private static PublicKey LoadPublicKey()
    {
        using var stream = typeof(Program).Assembly.GetManifestResourceStream(KeyResource)
                           ?? throw new InvalidOperationException($"Missing resource{KeyResource}");
        using var memory = new MemoryStream();
        stream.CopyTo(memory);

        return PublicKey.Import(
            SignatureAlgorithm.Ed25519, memory.ToArray(), KeyBlobFormat.PkixPublicKeyText);
    }
}
