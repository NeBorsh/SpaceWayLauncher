using System.Diagnostics;
using System.Security.Cryptography;
using Serilog;
using SpaceWay.Core.Localization;
using SpaceWay.Core.Util;

namespace SpaceWay.Core.Updates;

public enum UpdateState
{
    Idle,
    Checking,
    UpToDate,

    /// <summary>A newer release exists, but this copy cannot install it by itself.</summary>
    Available,

    Downloading,

    /// <summary>The installer is downloaded and verified; it runs when the launcher exits.</summary>
    Ready,

    Failed,
}

/// <summary>
/// Checks for new launcher releases. Installed copies download the new installer in the
/// background and run it after the launcher exits; portable and Linux copies only report
/// that an update exists.
/// </summary>
public sealed class UpdateService(
    IReleaseSource releases,
    HttpClient download,
    string updatesDirectory,
    bool canSelfUpdate,
    Version? current = null)
{
    private readonly Version _current = current ?? LauncherVersion.Current;

    public event Action? Changed;

    public UpdateState State { get; private set; }

    public LauncherRelease? Latest { get; private set; }

    public Exception? Error { get; private set; }

    public bool CanSelfUpdate => canSelfUpdate;

    /// <summary>Downloaded and verified installer, set in <see cref="UpdateState.Ready"/>.</summary>
    public string? InstallerPath { get; private set; }

    public async Task Check(CancellationToken cancel = default)
    {
        if (State is UpdateState.Checking or UpdateState.Downloading or UpdateState.Ready)
            return;

        Error = null;
        SetState(UpdateState.Checking);

        try
        {
            var latest = await releases.GetLatest(cancel);
            Latest = latest;

            if (latest == null || latest.Version <= _current)
            {
                Log.Information("Launcher is up to date ({Version})", _current.ToString(3));
                RemoveDownloads(keep: null);
                SetState(UpdateState.UpToDate);
                return;
            }

            Log.Information("Launcher update available: {Version}", latest.Tag);

            if (!canSelfUpdate)
            {
                SetState(UpdateState.Available);
                return;
            }

            SetState(UpdateState.Downloading);
            InstallerPath = await DownloadInstaller(latest, cancel);
            SetState(UpdateState.Ready);
        }
        catch (OperationCanceledException)
        {
            SetState(UpdateState.Idle);
        }
        catch (Exception e)
        {
            Log.Warning(e, "Update check failed");
            Error = e;
            SetState(UpdateState.Failed);
        }
    }

    /// <summary>
    /// The installer to run on exit, or null if there is nothing to install or the game is
    /// running: the game loader lives in the launcher folder and cannot be replaced while in use.
    /// </summary>
    public string? PendingInstaller()
    {
        if (State != UpdateState.Ready || InstallerPath == null || !File.Exists(InstallerPath))
            return null;

        if (IsGameRunning())
        {
            Log.Information("Game is running, launcher update postponed");
            return null;
        }

        return InstallerPath;
    }

    /// <summary>Runs the installer without any windows. It must start after the launcher releases its mutex.</summary>
    public static void StartInstaller(string path)
    {
        Log.Information("Starting launcher update: {Path}", path);

        Process.Start(new ProcessStartInfo(path, "/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /SP-")
        {
            UseShellExecute = false,
        })?.Dispose();
    }

    public static bool IsGameRunning()
    {
        var loaders = Process.GetProcessesByName("SpaceWay.Loader");
        var running = loaders.Length > 0;

        foreach (var loader in loaders)
            loader.Dispose();

        return running;
    }

    private async Task<string> DownloadInstaller(LauncherRelease release, CancellationToken cancel)
    {
        var name = release.WindowsInstallerName;

        var checksums = GitHubReleases.ParseChecksums(await download.GetStringAsync(
            release.FileUrl(LauncherRelease.ChecksumsFile), cancel));

        if (!checksums.TryGetValue(name, out var expected))
            throw new UpdateException("update-error-no-checksum");

        Directory.CreateDirectory(updatesDirectory);

        var target = Path.Combine(updatesDirectory, name);
        RemoveDownloads(keep: target);

        if (File.Exists(target) && await HashOf(target, cancel) == expected)
            return target;

        var partial = target + ".partial";

        await using (var file = File.Create(partial))
        {
            await download.DownloadToStream(release.FileUrl(name).AbsoluteUri, file, null, cancel);
        }

        if (await HashOf(partial, cancel) != expected)
        {
            File.Delete(partial);
            throw new UpdateException("update-error-checksum-mismatch");
        }

        File.Move(partial, target, overwrite: true);
        Log.Information("Launcher update downloaded: {Path}", target);

        return target;
    }

    private void RemoveDownloads(string? keep)
    {
        if (!Directory.Exists(updatesDirectory))
            return;

        foreach (var file in Directory.EnumerateFiles(updatesDirectory))
        {
            if (string.Equals(file, keep, StringComparison.OrdinalIgnoreCase))
                continue;

            try
            {
                File.Delete(file);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                Log.Debug(e, "Failed to delete old update {Path}", file);
            }
        }
    }

    private static async Task<string> HashOf(string path, CancellationToken cancel)
    {
        await using var stream = File.OpenRead(path);
        return Convert.ToHexStringLower(await SHA256.HashDataAsync(stream, cancel));
    }

    private void SetState(UpdateState state)
    {
        State = state;
        Changed?.Invoke();
    }
}

/// <summary>A launcher update failed for a reason that can be shown to the player.</summary>
public sealed class UpdateException(string key) : LocalizedException(key);
