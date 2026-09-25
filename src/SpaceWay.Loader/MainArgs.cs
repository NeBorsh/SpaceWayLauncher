using Robust.LoaderApi;

namespace SpaceWay.Loader;

/// <summary>Everything the loader passes to the engine at startup.</summary>
internal sealed record MainArgs(
    string[] Args,
    IFileApi FileApi,
    IRedialApi? RedialApi,
    IEnumerable<ApiMount>? ApiMounts) : IMainArgs;
