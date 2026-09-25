using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using SpaceWay.Core.Data;
using SpaceWay.Launcher.ViewModels;
using SpaceWay.Launcher.Views;

namespace SpaceWay.Launcher;

public sealed class App : Application
{
    private LauncherServices? _services;

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            _services = new LauncherServices();
            _services.ApplySavedLanguage();

            desktop.Exit += (_, _) =>
            {
                Program.PendingInstaller = _services.Updates.PendingInstaller();
                _services.Dispose();
            };

            var viewModel = new MainWindowViewModel(_services);

            var window = new MainWindow
            {
                DataContext = viewModel,
            };

            desktop.MainWindow = window;

            Program.Instance?.Listen(message =>
                Dispatcher.UIThread.Post(() => OnForwarded(message, window, viewModel)));

            _ = viewModel.Servers.RefreshAsync();

            if (Program.StartupConnect is { } target)
                _ = viewModel.ConnectFromLinkAsync(target);
            else
                _ = viewModel.OfferSignInAsync();

            if (_services.Settings.GetBool(SettingKeys.UpdatesCheck, true))
                _ = _services.Updates.Check();
        }

        base.OnFrameworkInitializationCompleted();
    }

    /// <summary>A later launch asked to show the window or to connect to a server.</summary>
    private static void OnForwarded(string message, Window window, MainWindowViewModel viewModel)
    {
        if (window.WindowState == WindowState.Minimized)
            window.WindowState = WindowState.Normal;

        window.Activate();

        if (message.StartsWith(SingleInstance.ConnectPrefix, StringComparison.Ordinal)
            && Uri.TryCreate(message[SingleInstance.ConnectPrefix.Length..], UriKind.Absolute, out var target))
        {
            _ = viewModel.ConnectFromLinkAsync(target);
        }
    }
}
