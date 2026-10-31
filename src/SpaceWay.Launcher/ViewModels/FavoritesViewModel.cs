using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Input;
using SpaceWay.Core.Favorites;
using SpaceWay.Core.Hubs;
using SpaceWay.Core.Localization;

namespace SpaceWay.Launcher.ViewModels;

public sealed partial class FavoritesViewModel : LocalizedViewModel
{
    private readonly FavoritesService _favorites;
    private readonly ServerDirectory _directory;
    private readonly ServerRefreshViewModel _refresh;
    private readonly DialogService _dialogs;
    private readonly Func<string, string, Task>? _connect;
    private readonly ServerCardState? _cards;

    public FavoritesViewModel(
        FavoritesService favorites,
        ServerDirectory directory,
        ServerRefreshViewModel refresh,
        DialogService dialogs,
        Func<string, string, Task>? connect = null,
        ServerCardState? cards = null)
    {
        _favorites = favorites;
        _directory = directory;
        _refresh = refresh;
        _dialogs = dialogs;
        _connect = connect;
        _cards = cards;

        _favorites.Changed += Reload;

        _directory.Updated += Reload;

        Reload();
    }

    public ObservableCollection<ServerItemViewModel> Items { get; } = [];

    public ServerRefreshViewModel Refresh => _refresh;

    public string CountText => Loc.T("servers-count", ("count", Items.Count));

    /// <summary>
    /// Favorites come from the local database, so a hub refresh never makes
    /// the list empty or non-empty; the hint stays visible while loading.
    /// </summary>
    public bool IsEmpty => Items.Count == 0;

    /// <summary>
    /// Adding a server bypassing hubs: address entered manually.
    /// </summary>
    [RelayCommand]
    private async Task AddServerAsync()
    {
        await _dialogs.ShowAsync(new AddServerDialogViewModel(_favorites));
    }

    protected override void OnLanguageChanged() => OnPropertyChanged(nameof(CountText));

    public override void Dispose()
    {
        _favorites.Changed -= Reload;
        _directory.Updated -= Reload;
        DisposeItems();
        base.Dispose();
    }

    private void Reload()
    {
        DisposeItems();

        foreach (var favorite in _favorites.Servers.OrderBy(s => s.SortOrder))
        {
            Items.Add(new ServerItemViewModel(
                favorite.Address,
                _directory.Find(favorite.Address),
                _favorites,
                favorite.DisplayName,
                _connect,
                _cards));
        }

        OnPropertyChanged(nameof(CountText));
        OnPropertyChanged(nameof(IsEmpty));
    }

    private void DisposeItems()
    {
        foreach (var item in Items)
            item.Dispose();

        Items.Clear();
    }
}
