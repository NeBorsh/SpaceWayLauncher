using CommunityToolkit.Mvvm.ComponentModel;
using SpaceWay.Core.Data;
using SpaceWay.Launcher.Theme;

namespace SpaceWay.Launcher.ViewModels;

public sealed partial class MainWindowViewModel : ObservableObject
{
    private readonly LauncherServices _services;

    [ObservableProperty]
    private NavigationSection _selectedSection;

    public MainWindowViewModel(LauncherServices services)
    {
        _services = services;

        var cards = new ServerCardState(services.ServerInfo);

        Servers = new ServersViewModel(
            services.Directory,
            services.HubManager,
            services.Settings,
            services.FavoritesService,
            ConnectToServer,
            cards);

        Favorites = new FavoritesViewModel(
            services.FavoritesService, services.Directory, Dialogs, ConnectToServer, cards);
        Accounts = new AccountsViewModel(services.AccountManager, Dialogs);
        HubsSection = new HubsViewModel(services.HubManager, Dialogs);
        Settings = new SettingsViewModel(services.Settings, services.ContentUpdater, Dialogs);
        Mods = new ModsViewModel(services.Mods, Dialogs);

        Sections =
        [
            new NavigationSection("nav-servers", NavIcons.Servers, Servers),
            new NavigationSection("nav-favorites", NavIcons.Favorites, Favorites),
            new NavigationSection("nav-accounts", NavIcons.Accounts, Accounts),
            new NavigationSection("nav-hubs", NavIcons.Hubs, HubsSection),
            new NavigationSection("nav-mods", NavIcons.Mods, Mods),
            new NavigationSection("nav-settings", NavIcons.Settings, Settings),
        ];

        _selectedSection = Sections[0];
    }

    public ServersViewModel Servers { get; }

    public FavoritesViewModel Favorites { get; }

    public AccountsViewModel Accounts { get; }

    public HubsViewModel HubsSection { get; }

    public SettingsViewModel Settings { get; }

    public ModsViewModel Mods { get; }

    /// <summary>Modal dialogs over the main window.</summary>
    public DialogService Dialogs { get; } = new();

    public IReadOnlyList<NavigationSection> Sections { get; }

    /// <summary>
    /// Offers to sign in if there are no accounts.
    /// </summary>
    public async Task OfferSignInAsync()
    {
        if (_services.AccountManager.Accounts.Count > 0)
            return;

        if (await Dialogs.ShowAsync(new SignInPromptViewModel()))
            await SignInAsync();
    }

    /// <summary>
    /// Launches a replay or content bundle from a file.
    /// </summary>
    public async Task OpenBundleAsync(string path)
    {
        if (Dialogs.IsOpen)
            return;

        var connection = new ConnectionViewModel(
            _services.Connector, Dialogs, path, Path.GetFileName(path),
            _services.Mods.EnabledFiles().Count, isBundle: true);

        var shown = Dialogs.ShowAsync(connection);

        await connection.Run();
        await shown;
    }

    /// <summary>Add-account wizard. True if an account was added.</summary>
    private Task<bool> SignInAsync() =>
        Dialogs.ShowAsync(new AddAccountDialogViewModel(_services.AccountManager));

    /// <summary>
    /// Shows the connection window and drives it to completion.
    /// </summary>
    private async Task ConnectToServer(string address, string serverName)
    {
        var connection = new ConnectionViewModel(
            _services.Connector, Dialogs, address, serverName, _services.Mods.EnabledFiles().Count, SignInAsync);

        var shown = Dialogs.ShowAsync(connection);

        await connection.Run();
        await shown;
    }
}
