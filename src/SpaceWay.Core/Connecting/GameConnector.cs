using System.Diagnostics;
using System.Text;
using Serilog;
using SpaceWay.Core.Accounts;
using SpaceWay.Core.Content;
using SpaceWay.Core.Data;
using SpaceWay.Core.Engine;
using SpaceWay.Core.Hubs;
using SpaceWay.Core.Mods;
using SpaceWay.Core.Util;

namespace SpaceWay.Core.Connecting;

/// <summary>
/// Connecting to a server: from an address to a running game.
/// </summary>
public sealed class GameConnector(
    IServerApi serverApi,
    ContentUpdater content,
    EngineManager engines,
    AccountManager accounts,
    PrivacyPolicyStore privacyPolicies,
    SettingsStore settings,
    ModOverlay? mods = null,
    string? loaderPath = null)
{
    /// <summary>Name used when joining with no account at all.</summary>
    public const string FallbackUsername = "SpaceWayPlayer";

    /// <summary>How many recent game runs to keep logs for.</summary>
    private const int KeptClientLogs = 10;

    /// <summary>Errors meaning "the bundle's base build cannot be downloaded".</summary>
    private static readonly HashSet<string> BaseBuildFetchErrors =
    [
        "error-manifest-fetch-failed",
        "error-zip-fetch-failed",
        "error-no-content-source",
    ];

    private readonly string _loaderPath = loaderPath ?? LauncherPaths.PathLoader;

    /// <param name="useMods">
    /// Whether to apply enabled mods. The retry after a sandbox failure runs
    /// without mods, since with them it would fail the same way.
    /// </param>
    public async Task<GameSession> Connect(
        string address,
        IProgress<ContentProgress>? progress = null,
        IPrivacyPolicyPrompt? privacyPrompt = null,
        bool useMods = true,
        CancellationToken cancel = default)
    {
        if (!ServerAddress.TryParse(address, out var serverAddress))
            throw new ConnectException("error-bad-server-address", null, ("address", address));

        progress?.Report(new ContentProgress(ContentStage.AskingServer));

        var info = await serverApi.GetInfo(serverAddress, cancel);

        if (info.Build == null)
            throw new ConnectException("error-no-build-info");

        await EnsurePrivacyPolicyAccepted(info, privacyPrompt, cancel);

        var account = ResolveAccount(info);
        var token = account == null ? null : await accounts.EnsureValidTokenAsync(account, cancel);

        if (RequiresAuth(info) && token == null)
            throw new ConnectException("error-auth-login-failed");

        var launch = await content.Prepare(info.Build, progress, cancel);

        progress?.Report(new ContentProgress(ContentStage.StartingGame));

        var overlay = useMods ? mods?.Build(DateTimeOffset.UtcNow) : null;

        return Start(
            BuildLaunchPlan(serverAddress, info, launch, account, token, overlay),
            serverAddress);
    }

    /// <summary>
    /// Launches a content bundle or a replay: the game without a server.
    /// </summary>
    public async Task<GameSession> LaunchBundle(
        string path,
        IProgress<ContentProgress>? progress = null,
        bool useMods = true,
        CancellationToken cancel = default)
    {
        progress?.Report(new ContentProgress(ContentStage.OpeningBundle));

        ContentLaunchInfo launch;
        string? bundleOverlay = null;
        bool serverGc;

        using (var bundle = await Task.Run(() => ContentBundle.Open(path), cancel))
        {
            serverGc = bundle.Metadata.ServerGC == true;

            try
            {
                if (bundle.MountsAsOverlay)
                {
                    Log.Information("Running bundle on top of the base build");
                    launch = await content.Prepare(bundle.Metadata.BaseBuildInformation(), progress, cancel);
                    bundleOverlay = path;
                }
                else
                {
                    Log.Information("Unpacking bundle into the content database");
                    launch = await content.PrepareBundle(bundle, progress, cancel);
                }
            }
            catch (ContentUpdateException e)
                when (bundle.Metadata.BaseBuild is { } baseBuild && BaseBuildFetchErrors.Contains(e.Key))
            {
                throw new ContentUpdateException("error-bundle-base-unavailable", e,
                    ("fork", baseBuild.ForkId), ("version", baseBuild.Version));
            }
        }

        progress?.Report(new ContentProgress(ContentStage.StartingGame));

        var overlay = useMods ? mods?.Build(DateTimeOffset.UtcNow) : null;

        return Start(
            BuildBundleLaunchPlan(launch, bundleOverlay, overlay, serverGc),
            new Uri(Path.GetFullPath(path)));
    }

    /// <summary>
    /// Obtains consent to the server's privacy policy, if it has one.
    /// </summary>
    private async Task EnsurePrivacyPolicyAccepted(
        ServerInfo info,
        IPrivacyPolicyPrompt? prompt,
        CancellationToken cancel)
    {
        if (info.PrivacyPolicy is not { } policy)
            return;

        var accepted = privacyPolicies.AcceptedVersion(policy.Identifier);

        if (accepted == policy.Version)
        {
            privacyPolicies.Touch(policy.Identifier);
            return;
        }

        if (prompt == null)
        {
            throw new ConnectException("error-privacy-nobody-to-ask");
        }

        Log.Information(
            "Asking consent to policy {Identifier} version {Version}",
            policy.Identifier, policy.Version);

        if (!await prompt.Ask(policy, versionChanged: accepted != null, cancel))
            throw new ConnectException("error-privacy-declined");

        privacyPolicies.Accept(policy.Identifier, policy.Version);
    }

    /// <summary>
    /// Chooses which identity to join as.
    /// </summary>
    private Account? ResolveAccount(ServerInfo info)
    {
        if (info.Auth?.Mode == AuthMode.Disabled)
            return accounts.Selected;

        var selected = accounts.Selected;

        if (selected == null && RequiresAuth(info))
            throw new ConnectException("error-auth-no-account");

        return selected;
    }

    private static bool RequiresAuth(ServerInfo info) => info.Auth?.Mode == AuthMode.Required;

    /// <summary>
    /// Collects everything the loader will be started with.
    /// </summary>
    public GameLaunchPlan BuildLaunchPlan(
        Uri serverAddress,
        ServerInfo info,
        ContentLaunchInfo launch,
        Account? account,
        AuthToken? token,
        string? overlayPath = null)
    {
        var arguments = EngineHead(launch);
        arguments.AddRange(EngineArguments(serverAddress, info, account));
        AddCompat(arguments);

        var environment = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (name, value) in Environment(launch, info, token, account, overlayPath))
            environment[name] = value;

        return new GameLaunchPlan(_loaderPath, arguments, environment);
    }

    /// <summary>Builds a bundle launch. See <see cref="BuildLaunchPlan"/>.</summary>
    /// <param name="bundleOverlay">Bundle path, if it is mounted over the base build.</param>
    /// <param name="serverGc">
    /// The bundle requests the server garbage collector. Replays do this because
    /// seeking creates and discards large numbers of objects.
    /// </param>
    public GameLaunchPlan BuildBundleLaunchPlan(
        ContentLaunchInfo launch,
        string? bundleOverlay,
        string? modsOverlay = null,
        bool serverGc = false)
    {
        var arguments = EngineHead(launch);

        arguments.Add("--username");
        arguments.Add(accounts.Selected?.Username ?? FallbackUsername);

        arguments.Add("--cvar");
        arguments.Add("launch.launcher=true");

        arguments.Add("--cvar");
        arguments.Add("launch.content_bundle=true");

        AddCompat(arguments);

        var environment = CommonEnvironment(launch, modsOverlay)
            .ToDictionary(p => p.Name, p => p.Value, StringComparer.Ordinal);

        if (bundleOverlay != null)
            environment["SPACEWAY_BUNDLE_ZIP"] = bundleOverlay;

        if (serverGc)
            environment["DOTNET_gcServer"] = "1";

        return new GameLaunchPlan(_loaderPath, arguments, environment);
    }

    /// <summary>Leading arguments form the contract with the loader: what to run and how to verify it.</summary>
    private List<string> EngineHead(ContentLaunchInfo launch)
    {
        var engineVersion = launch.Modules
            .Single(m => m.Name == ContentStore.RobustModuleName).Version;

        return [engines.GetEnginePath(engineVersion), engines.GetEngineSignature(engineVersion)];
    }

    /// <summary>
    /// Compatibility mode, the only launch option the launcher adds on its own.
    /// Passed only when the player asks for it: the official launcher always sends it,
    /// which needlessly degrades graphics on modern hardware.
    /// </summary>
    private void AddCompat(List<string> arguments)
    {
        if (!settings.GetBool(SettingKeys.DisplayCompat, false))
            return;

        arguments.Add("--cvar");
        arguments.Add("display.compat=true");
    }

    private GameSession Start(GameLaunchPlan plan, Uri serverAddress)
    {
        if (!File.Exists(plan.LoaderPath))
            throw new ConnectException("error-loader-missing", null, ("path", plan.LoaderPath));

        var startInfo = new ProcessStartInfo
        {
            FileName = plan.LoaderPath,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };

        foreach (var argument in plan.Arguments)
            startInfo.ArgumentList.Add(argument);

        foreach (var (name, value) in plan.Environment)
            startInfo.EnvironmentVariables[name] = value;

        Log.Information("Launching game: server {Address}", serverAddress);

        var process = Process.Start(startInfo)
                      ?? throw new ConnectException("error-loader-start-failed");

        var (stdout, stderr) = PipeOutputToLogs(process);

        return new GameSession(process, serverAddress, stdout, stderr, plan.HasMods);
    }

    /// <summary>Command-line arguments for the engine itself.</summary>
    private static IEnumerable<string> EngineArguments(
        Uri serverAddress,
        ServerInfo info,
        Account? account)
    {
        yield return "--username";
        yield return account?.Username ?? FallbackUsername;

        yield return "--cvar";
        yield return "launch.launcher=true";

        yield return "--launcher";

        yield return "--connect-address";
        yield return ConnectAddress(info, serverAddress).ToString();

        yield return "--ss14-address";
        yield return serverAddress.ToString();

        foreach (var argument in BuildCVars(info.Build!))
            yield return argument;
    }

    private static IEnumerable<string> BuildCVars(ServerBuildInformation build)
    {
        foreach (var (name, value) in new (string, string?)[]
                 {
                     ("download_url", build.DownloadUrl),
                     ("manifest_url", build.ManifestUrl),
                     ("manifest_download_url", build.ManifestDownloadUrl),
                     ("version", build.Version),
                     ("fork_id", build.ForkId),
                     ("hash", build.Hash),
                     ("manifest_hash", build.ManifestHash),
                     ("engine_version", build.EngineVersion),
                 })
        {
            if (string.IsNullOrEmpty(value))
                continue;

            yield return "--cvar";
            yield return $"build.{name}={value}";
        }
    }

    private IEnumerable<(string Name, string Value)> Environment(
        ContentLaunchInfo launch,
        ServerInfo info,
        AuthToken? token,
        Account? account,
        string? overlayPath)
    {
        foreach (var variable in CommonEnvironment(launch, overlayPath))
            yield return variable;

        if (token == null || account == null || info.Auth?.Mode == AuthMode.Disabled)
            yield break;

        var authServer = accounts.FindServer(account.AuthServerId);
        if (authServer == null || authServer.IsOffline)
            yield break;

        yield return ("ROBUST_AUTH_TOKEN", token.Value);
        yield return ("ROBUST_AUTH_USERID", account.UserId.ToString());
        yield return ("ROBUST_AUTH_SERVER", authServer.Address.ToString());

        if (!string.IsNullOrEmpty(info.Auth?.PublicKey))
            yield return ("ROBUST_AUTH_PUBKEY", info.Auth.PublicKey);
    }

    /// <summary>Environment shared by server and bundle launches: content, mods, engine modules.</summary>
    private IEnumerable<(string Name, string Value)> CommonEnvironment(
        ContentLaunchInfo launch,
        string? overlayPath)
    {
        yield return ("SPACEWAY_CONTENT_DB", LauncherPaths.PathContentDb);

        if (overlayPath != null)
            yield return ("SPACEWAY_OVERLAY_ZIP", overlayPath);
        yield return ("SPACEWAY_CONTENT_VERSION", launch.VersionId.ToString());

        yield return ("SPACEWAY_LAUNCHER_PATH", Process.GetCurrentProcess().MainModule?.FileName ?? "");

        foreach (var (name, version) in launch.Modules)
        {
            if (name == ContentStore.RobustModuleName)
                continue;

            var variable = $"ROBUST_MODULE_{name.ToUpperInvariant().Replace('.', '_')}";
            yield return (variable, engines.GetModulePath(name, version));
        }
    }

    /// <summary>
    /// Where the game connection should go.
    /// </summary>
    private static Uri ConnectAddress(ServerInfo info, Uri serverAddress)
    {
        if (string.IsNullOrEmpty(info.ConnectAddress))
        {
            return new UriBuilder
            {
                Scheme = "udp",
                Host = serverAddress.Host,
                Port = serverAddress.IsDefaultPort ? ServerAddress.DefaultPort : serverAddress.Port,
            }.Uri;
        }

        if (!Uri.TryCreate(info.ConnectAddress, UriKind.Absolute, out var connect))
            throw new ConnectException("error-bad-connect-address", null,
                ("address", info.ConnectAddress ?? string.Empty));

        return connect;
    }

    /// <summary>
    /// Redirects game output to files.
    /// </summary>
    private static (string Stdout, string Stderr) PipeOutputToLogs(Process process)
    {
        FileHelpers.EnsureDirectoryExists(LauncherPaths.DirLogs);

        var (stdout, stderr) = LauncherPaths.ClientLogPaths(DateTimeOffset.Now);

        Log.Debug("Writing game output to {Path}", stdout);

        Pipe(process.StandardOutput, stdout);
        Pipe(process.StandardError, stderr);

        PruneOldClientLogs();

        return (stdout, stderr);

        static void Pipe(StreamReader source, string path)
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    await using var file = new FileStream(
                        path, FileMode.Create, FileAccess.Write,
                        FileShare.ReadWrite | FileShare.Delete,
                        4096, FileOptions.Asynchronous);

                    await source.BaseStream.CopyToAsync(file);
                }
                catch (Exception e)
                {
                    Log.Warning(e, "Failed to write game output to {Path}", path);
                }
            });
        }
    }

    /// <summary>
    /// Removes logs of old runs.
    /// </summary>
    private static void PruneOldClientLogs()
    {
        try
        {
            foreach (var path in LauncherPaths.ClientLogFiles().Skip(KeptClientLogs * 2))
            {
                Log.Debug("Removing old game log {Path}", path);
                File.Delete(path);
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            Log.Debug(e, "Failed to remove old game logs");
        }
    }
}

/// <summary>
/// What the game loader will be started with.
/// </summary>
/// <param name="LoaderPath">Executable to run.</param>
/// <param name="Arguments">Engine path, its signature and engine arguments.</param>
/// <param name="Environment">Token, content and engine module paths.</param>
public sealed record GameLaunchPlan(
    string LoaderPath,
    IReadOnlyList<string> Arguments,
    IReadOnlyDictionary<string, string> Environment)
{
    /// <summary>
    /// Whether the game runs with mods. The connection window uses this to decide
    /// whether to offer a retry without them.
    /// </summary>
    public bool HasMods => Environment.ContainsKey("SPACEWAY_OVERLAY_ZIP");
}

/// <summary>A running game.</summary>
public sealed class GameSession(
    Process process,
    Uri serverAddress,
    string? stdoutLog = null,
    string? stderrLog = null,
    bool hasMods = false) : IDisposable
{
    public Uri ServerAddress => serverAddress;

    /// <summary>Whether the game runs with mods.</summary>
    public bool HasMods => hasMods;
    public int ProcessId => process.Id;
    public bool HasExited => process.HasExited;
    public int ExitCode => process.ExitCode;

    /// <summary>
    /// Waits for the game to exit.
    /// </summary>
    public Task WaitForExit(CancellationToken cancel = default) => process.WaitForExitAsync(cancel);

    /// <summary>
    /// Whether the game crashed right after starting.
    /// </summary>
    public async Task<bool> DiedOnStartup(TimeSpan within, CancellationToken cancel = default)
    {
        try
        {
            await process.WaitForExitAsync(cancel).WaitAsync(within, cancel);
        }
        catch (TimeoutException)
        {
            return false;
        }

        return process.ExitCode != 0;
    }

    /// <summary>
    /// Reads the log to find out why the game died on startup.
    /// </summary>
    public StartupFailure DiagnoseFailure() =>
        StartupDiagnosis.Diagnose(stderrLog, stdoutLog);

    public void Dispose() => process.Dispose();
}
