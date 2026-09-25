namespace SpaceWay.Core.Engine;

/// <summary>A downloaded engine build.</summary>
/// <param name="Version">Robust version, e.g. "240.1.0".</param>
/// <param name="Signature">Ed25519 signature of the archive in hex, from the manifest.</param>
public sealed record InstalledEngine(string Version, string Signature);

/// <summary>A downloaded engine module, extracted to disk.</summary>
public sealed record InstalledEngineModule(string Name, string Version);
