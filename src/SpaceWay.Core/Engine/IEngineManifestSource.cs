namespace SpaceWay.Core.Engine;

/// <summary>Source of engine build manifests.</summary>
public interface IEngineManifestSource
{
    Task<EngineBuildManifest> GetBuilds(CancellationToken cancel = default);
    Task<EngineModuleManifest> GetModules(CancellationToken cancel = default);
}
