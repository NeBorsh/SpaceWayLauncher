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
    private readonly DialogService _dialogs;
    private readonly Func<string, string, Task>? _connect;
    private readonly ServerCardState? _cards;

    public FavoritesViewModel(
        FavoritesService favorites,
        ServerDirectory directory,
        DialogService dialogs,
        Func<string, string, Task>? connect = null,
        ServerCardState? cards = null)
    {
        _favorites = favorites;
        _directory = directory;
        _dialogs = dialogs;
        _connect = connect;
        _cards = cards;

        _favorites.Changed += Reload;

        _directory.Updated += Reload;

        Reload();
    }

    public ObservableCollection<ServerItemViewModel> Items { get; } = [];

    public string CountText => Loc.T("servers-count", ("count", Items.Count));

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

    private void Reload()
    {
        Items.Clear();

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
}
