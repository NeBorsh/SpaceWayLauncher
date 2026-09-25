using System.IO.Pipes;
using System.Runtime.InteropServices;
using Serilog;

namespace SpaceWay.Launcher;

/// <summary>
/// Keeps one launcher per user. A second launch hands its request to the running one over
/// a named pipe that only the current user can open, then exits.
/// </summary>
internal sealed class SingleInstance : IDisposable
{
    public const string ActivateMessage = "activate";
    public const string ConnectPrefix = "connect ";

    private const string MutexName = "SpaceWayLauncher";

    /// <summary>Held by the installer while it runs; see <c>SetupMutex</c> in the installer script.</summary>
    private const string SetupMutexName = "SpaceWayLauncherSetup";

    private static readonly TimeSpan ForwardTimeout = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan SetupWaitLimit = TimeSpan.FromMinutes(2);

    private readonly Mutex _mutex;
    private readonly CancellationTokenSource _stop = new();

    private SingleInstance(Mutex mutex) => _mutex = mutex;

    private static string PipeName => $"SpaceWayLauncher-{Environment.UserName}";

    public static SingleInstance? TryAcquire()
    {
        var mutex = new Mutex(initiallyOwned: true, MutexName, out var mine);

        if (mine)
            return new SingleInstance(mutex);

        mutex.Dispose();
        return null;
    }

    /// <summary>
    /// Sends a message to the running launcher. This process was started by the user, so it
    /// lets the running launcher take the foreground; Windows would refuse it otherwise.
    /// </summary>
    public static bool Forward(string message)
    {
        if (OperatingSystem.IsWindows())
            AllowSetForegroundWindow(AnyProcess);

        try
        {
            using var pipe = new NamedPipeClientStream(
                ".", PipeName, PipeDirection.Out, PipeOptions.CurrentUserOnly);

            pipe.Connect(ForwardTimeout);

            using var writer = new StreamWriter(pipe);
            writer.WriteLine(message);

            return true;
        }
        catch (Exception e) when (e is TimeoutException or IOException or UnauthorizedAccessException)
        {
            Log.Warning(e, "Failed to reach the running launcher");
            return false;
        }
    }

    /// <summary>
    /// Waits for a running update installer to finish.
    /// </summary>
    /// <returns>Whether an installer was running, so this process may be the old version.</returns>
    public static bool WaitForUpdateInstaller()
    {
        if (!Mutex.TryOpenExisting(SetupMutexName, out var setup))
            return false;

        setup.Dispose();
        Log.Information("Launcher update is being installed, waiting for it");

        var deadline = DateTime.UtcNow + SetupWaitLimit;

        while (DateTime.UtcNow < deadline && Mutex.TryOpenExisting(SetupMutexName, out setup))
        {
            setup.Dispose();
            Thread.Sleep(500);
        }

        return true;
    }

    /// <summary>Receives messages from later launches until disposed.</summary>
    public void Listen(Action<string> onMessage) => _ = Task.Run(async () =>
    {
        while (!_stop.IsCancellationRequested)
        {
            try
            {
                await using var server = new NamedPipeServerStream(
                    PipeName, PipeDirection.In, 1, PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);

                await server.WaitForConnectionAsync(_stop.Token);

                using var reader = new StreamReader(server);

                if (await reader.ReadLineAsync(_stop.Token) is { } message)
                    onMessage(message);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (IOException e)
            {
                Log.Warning(e, "Launcher message channel failed");
            }
        }
    });

    private const int AnyProcess = -1;

    [DllImport("user32.dll")]
    private static extern bool AllowSetForegroundWindow(int processId);

    public void Dispose()
    {
        _stop.Cancel();
        _mutex.ReleaseMutex();
        _mutex.Dispose();
    }
}
