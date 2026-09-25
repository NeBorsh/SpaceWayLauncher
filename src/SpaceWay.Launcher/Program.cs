using Avalonia;
using Serilog;
using SpaceWay.Core;
using SpaceWay.Core.Updates;

namespace SpaceWay.Launcher;

internal static class Program
{
    /// <summary>Downloaded launcher installer to run once the launcher has exited.</summary>
    public static string? PendingInstaller { get; set; }

    [STAThread]
    public static void Main(string[] args)
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;

#if DEBUG
        System.Diagnostics.Trace.Listeners.Add(new System.Diagnostics.ConsoleTraceListener());
#endif

        Directory.CreateDirectory(LauncherPaths.DirLogs);

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.Console()
            .WriteTo.File(
                Path.Combine(LauncherPaths.DirLogs, "launcher.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 5)
            .CreateLogger();

        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            Log.Fatal(e.ExceptionObject as Exception, "Launcher crashed");
            Log.CloseAndFlush();
        };

        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            Log.Error(e.Exception, "Unhandled exception in a background task");
            e.SetObserved();
        };

        var single = SingleInstance();
        if (single == null)
        {
            Log.Information("Launcher is already running");
            return;
        }

        try
        {
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
        catch (Exception e)
        {
            Log.Fatal(e, "Launcher crashed on startup");
            throw;
        }
        finally
        {
            single.ReleaseMutex();
            single.Dispose();

            if (PendingInstaller != null)
            {
                try
                {
                    UpdateService.StartInstaller(PendingInstaller);
                }
                catch (Exception e)
                {
                    Log.Error(e, "Failed to start launcher update");
                }
            }

            Log.CloseAndFlush();
        }
    }

    /// <summary>
    /// Claims the single-instance slot, or fails if another launcher holds it.
    /// </summary>
    private static Mutex? SingleInstance()
    {
        var mutex = new Mutex(initiallyOwned: true, "SpaceWayLauncher", out var mine);

        if (mine)
            return mutex;

        mutex.Dispose();
        return null;
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .LogToTrace();
}
