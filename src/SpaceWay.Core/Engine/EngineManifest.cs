using System.Text.Json.Serialization;
using SpaceWay.Core.Localization;

namespace SpaceWay.Core.Engine;

/// <summary>
/// Engine build manifest: version to download URLs per platform.
/// </summary>
public sealed record EngineBuildManifest(Dictionary<string, EngineVersionInfo> Versions)
{
    /// <summary>
    /// Looks up a version, following the redirect chain.
    /// </summary>
    public FoundEngineVersion? Find(string version)
    {
        for (var step = 0; step < 16; step++)
        {
            if (!Versions.TryGetValue(version, out var info))
                return null;

            if (info.RedirectVersion == null)
                return new FoundEngineVersion(version, info);

            version = info.RedirectVersion;
        }

        throw new EngineUpdateException("error-engine-redirect-loop", ("version", version));
    }
}

public sealed record FoundEngineVersion(string Version, EngineVersionInfo Info);

public sealed record EngineVersionInfo(
    bool Insecure,
    [property: JsonPropertyName("redirect")] string? RedirectVersion,
    Dictionary<string, EngineBuildInfo> Platforms);

public sealed record EngineBuildInfo(
    [property: JsonPropertyName("url")] string Url,
    [property: JsonPropertyName("sha256")] string Sha256,
    [property: JsonPropertyName("sig")] string Signature);

/// <summary>Engine module manifest.</summary>
public sealed record EngineModuleManifest(Dictionary<string, EngineModuleData> Modules)
{
    /// <summary>
    /// Picks the module version matching the engine version.
    /// </summary>
    public string ResolveVersion(string moduleName, string engineVersion)
    {
        if (!Modules.TryGetValue(moduleName, out var module))
            throw new EngineUpdateException($"Module {moduleName} is not in the manifest");

        var engine = Version.Parse(engineVersion);

        var suitable = module.Versions.Keys
            .Select(key => (Key: key, Version: Version.Parse(key)))
            .Where(v => engine >= v.Version)
            .ToList();

        if (suitable.Count == 0)
            throw new EngineUpdateException(
                $"No version of module {moduleName} matches engine {engineVersion}");

        return suitable.MaxBy(v => v.Version).Key;
    }
}

public sealed record EngineModuleData(Dictionary<string, EngineModuleVersionData> Versions);

public sealed record EngineModuleVersionData(
    Dictionary<string, EngineBuildInfo> Platforms,
    bool Insecure = false);

/// <summary>Engine update failed for a reason that can be shown to the player.</summary>
public sealed class EngineUpdateException(
    string key,
    params (string Name, object Value)[] args)
    : LocalizedException(key, null, args);

/// <summary>There is no engine build for this platform.</summary>
public sealed class NoEngineForPlatformException(
    string key,
    params (string Name, object Value)[] args)
    : LocalizedException(key, null, args);
