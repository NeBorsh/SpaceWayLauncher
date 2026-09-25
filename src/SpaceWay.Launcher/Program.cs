using System.Diagnostics;
using Avalonia;
using Serilog;
using SpaceWay.Core;
using SpaceWay.Core.Connecting;
using SpaceWay.Core.Updates;

namespace SpaceWay.Launcher;

internal static class Program
{
    /// <summary>Downloaded launcher installer to run once the launcher has exited.</summary>
    public static string? PendingInstaller { get; set; }

    /// <summary>Server from the command line to connect to on startup.</summary>
    public static Uri? StartupConnect { get; private set; }

    public static SingleInstance? Instance { get; private set; }

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

        if (SingleInstance.WaitForUpdateInstaller() && Environment.ProcessPath is { } self)
        {
            Log.Information("Restarting after the launcher update");
            RestartSelf(self, args);
            return;
        }

        StartupConnect = LaunchArguments.ConnectTarget(args);

        var single = SingleInstance.TryAcquire();
        if (single == null)
        {
            Log.Information("Launcher is already running, forwarding the request");
            SingleInstance.Forward(StartupConnect != null
                ? SingleInstance.ConnectPrefix + StartupConnect.AbsoluteUri
                : SingleInstance.ActivateMessage);
            return;
        }

        Instance = single;

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

    private static void RestartSelf(string path, string[] args)
    {
        var start = new ProcessStartInfo(path) { UseShellExecute = false };

        foreach (var arg in args)
            start.ArgumentList.Add(arg);

        Process.Start(start)?.Dispose();
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .LogToTrace();
}
