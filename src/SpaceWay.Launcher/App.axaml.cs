using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
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

            desktop.Exit += (_, _) => _services.Dispose();

            var viewModel = new MainWindowViewModel(_services);

            desktop.MainWindow = new MainWindow
            {
                DataContext = viewModel,
            };

            _ = viewModel.Servers.RefreshAsync();
            _ = viewModel.OfferSignInAsync();
        }

        base.OnFrameworkInitializationCompleted();
    }
}
