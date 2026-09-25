using System.Diagnostics.CodeAnalysis;
using System.IO.Compression;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Loader;
using Robust.LoaderApi;

namespace SpaceWay.Loader;

/// <summary>
/// Loads engine assemblies directly from the archive, bypassing the disk.
/// </summary>
internal sealed class EngineAssemblies : IDisposable
{
    /// <summary>Engine assembly everything starts from.</summary>
    public const string RobustAssemblyName = "Robust.Client";

    /// <summary>
    /// Contract between the loader and the engine. Never loaded from the archive.
    /// </summary>
    private const string ContractAssemblyName = "Robust.LoaderApi";

    private readonly ZipFileApi _files;
    private readonly Func<AssemblyLoadContext, AssemblyName, Assembly?> _resolving;
    private readonly Func<Assembly, string, IntPtr> _resolvingNative;

    public EngineAssemblies(string enginePath)
    {
        var prefix = RuntimeInformation.IsOSPlatform(OSPlatform.OSX)
            ? "Space Station 14.app/Contents/Resources/"
            : "";

        _files = new ZipFileApi(new ZipArchive(File.OpenRead(enginePath), ZipArchiveMode.Read), prefix);

        _resolving = (_, name) => TryLoad(name.Name!, out var assembly) ? assembly : null;
        _resolvingNative = ResolveNative;

        AssemblyLoadContext.Default.Resolving += _resolving;
        AssemblyLoadContext.Default.ResolvingUnmanagedDll += _resolvingNative;
    }

    /// <summary>Engine files, also read by the engine itself.</summary>
    public IFileApi Files => _files;

    /// <summary>Loads an engine assembly by name, without extension.</summary>
    public bool TryLoad(string name, [NotNullWhen(true)] out Assembly? assembly)
    {
        assembly = null;

        if (name == ContractAssemblyName)
        {
            Console.Error.WriteLine(
                $"Engine requested {ContractAssemblyName} from the archive, so the bundled copy did not match." +
                "Most likely a version mismatch.");
            return false;
        }

        if (!_files.TryOpen($"{name}.dll", out var code))
            return false;

        _files.TryOpen($"{name}.pdb", out var symbols);

        using (code)
        using (symbols)
        {
            assembly = AssemblyLoadContext.Default.LoadFromStream(code, symbols);
        }

        return true;
    }

    /// <summary>
    /// Finds the entry point the engine declares with an assembly attribute.
    /// </summary>
    public bool TryGetEntryPoint(Assembly client, [NotNullWhen(true)] out ILoaderEntryPoint? entryPoint)
    {
        entryPoint = null;

        var attribute = client.GetCustomAttribute<LoaderEntryPointAttribute>();
        if (attribute == null)
        {
            Console.Error.WriteLine("Engine assembly has no LoaderEntryPointAttribute");
            return false;
        }

        if (!attribute.LoaderEntryPointType.IsAssignableTo(typeof(ILoaderEntryPoint)))
        {
            Console.Error.WriteLine($"Type {attribute.LoaderEntryPointType} does not implement ILoaderEntryPoint");
            return false;
        }

        entryPoint = (ILoaderEntryPoint)Activator.CreateInstance(attribute.LoaderEntryPointType)!;
        return true;
    }

    public void Dispose()
    {
        AssemblyLoadContext.Default.Resolving -= _resolving;
        AssemblyLoadContext.Default.ResolvingUnmanagedDll -= _resolvingNative;

        _files.Dispose();
    }

    /// <summary>
    /// Resolves native libraries next to the loader.
    /// </summary>
    private static IntPtr ResolveNative(Assembly assembly, string name)
    {
        var path = Path.Combine(AppContext.BaseDirectory, name);

        return NativeLibrary.TryLoad(path, out var handle) ? handle : IntPtr.Zero;
    }
}
