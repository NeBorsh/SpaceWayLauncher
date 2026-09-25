using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Serilog;
using SpaceWay.Core.Favorites;
using SpaceWay.Core.Hubs;
using SpaceWay.Core.Localization;
using SpaceWay.Core.Util;
using SpaceWay.Launcher.Theme;

namespace SpaceWay.Launcher.ViewModels;

/// <summary>
/// Server card, shared by the server list and favorites.
/// </summary>
public sealed partial class ServerItemViewModel : LocalizedViewModel
{
    private readonly MergedServer? _server;
    private readonly FavoritesService _favorites;
    private readonly string _fallbackName;

    /// <summary>
    /// Action for the Play button.
    /// </summary>
    private readonly Func<string, string, Task>? _connect;

    private readonly ServerCardState? _cards;

    /// <summary>
    /// Whether the card passed the filters. Cards that did not are hidden rather
    /// than removed, since recreating cards on every search keystroke is expensive.
    /// </summary>
    [ObservableProperty] private bool _isShown = true;

    [ObservableProperty] private bool _isExpanded;
    [ObservableProperty] private bool _isLoadingDetails;
    [ObservableProperty] private string? _description;

    /// <summary>
    /// Why the server returned no details. The exception is kept rather than text,
    /// so the message is built on read and survives a language change.
    /// </summary>
    private Exception? _detailsError;

    /// <summary>The server responded and its description and links are shown.</summary>
    private bool _detailsLoaded;

    public ServerItemViewModel(
        MergedServer server,
        FavoritesService favorites,
        Func<string, string, Task>? connect = null,
        ServerCardState? cards = null)
        : this(server.Address, server, favorites, server.DisplayName, connect, cards)
    {
    }

    public ServerItemViewModel(
        string address,
        MergedServer? server,
        FavoritesService favorites,
        string fallbackName,
        Func<string, string, Task>? connect = null,
        ServerCardState? cards = null)
    {
        Address = address;
        _server = server;
        _favorites = favorites;
        _fallbackName = fallbackName;
        _connect = connect;
        _cards = cards;

        if (cards?.IsExpanded(address) == true)
        {
            _isExpanded = true;
            _ = LoadDetails();
        }
    }

    /// <summary>Server links. Web links only; others are dropped.</summary>
    public ObservableCollection<ServerLinkViewModel> Links { get; } = [];

    public bool HasLinks => Links.Count > 0;

    public bool HasDescription => !string.IsNullOrWhiteSpace(Description);

    public string DetailsErrorText => _detailsError?.Message ?? string.Empty;

    public bool HasDetailsError => _detailsError != null;

    /// <summary>
    /// The server responded but has neither description nor links. Stated
    /// explicitly, since an empty expanded card looks like it failed to load.
    /// </summary>
    public bool IsDetailsEmpty => _detailsLoaded && !HasDescription && !HasLinks;

    /// <summary>
    /// Whether the server can be joined.
    /// </summary>
    public bool CanConnect => _connect != null;

    public string Address { get; }

    /// <summary>What the hub reported about the server. Absent if the server is not listed.</summary>
    public MergedServer? Server => _server;

    public bool IsOffline => _server == null;

    public string Name
    {
        get
        {
            var custom = Favorite?.CustomName;
            if (!string.IsNullOrWhiteSpace(custom))
                return custom;

            return _server?.DisplayName ?? _fallbackName;
        }
    }

    /// <summary>
    /// Players and cap, e.g. "12 / 50". A single number without the cap does not
    /// tell whether there is room left.
    /// </summary>
    public string SlotsText
    {
        get
        {
            if (_server is not { } server)
                return Loc.T("server-offline");

            return server.Status.SoftMaxPlayers > 0
                ? Loc.T("server-slots",
                    ("players", server.Status.Players),
                    ("max", server.Status.SoftMaxPlayers))
                : Loc.T("server-slots-unlimited", ("players", server.Status.Players));
        }
    }

    public string RoundText => _server == null ? string.Empty : FormatRound(_server);

    public IReadOnlyList<string> Tags => _server?.AllTags ?? Favorite?.Tags ?? [];

    /// <summary>
    /// Hubs advertising the server. Always shown so the player knows where
    /// the entry came from, especially with several hubs.
    /// </summary>
    public string SourceText => _server is { } server
        ? Loc.T("servers-source", ("hubs", string.Join(", ", server.Sources.Select(h => h.DisplayName))))
        : string.Empty;

    public bool HasSource => _server != null;

    public string? Note => Favorite?.Note;

    public bool HasNote => !string.IsNullOrWhiteSpace(Note);

    public bool IsFull => _server?.IsFull ?? false;

    public bool IsFavorite => _favorites.IsFavorite(Address);

    private FavoriteServer? Favorite => _favorites.Find(Address);

    /// <summary>
    /// The server-reported name is saved when adding, so if the server leaves the hub
    /// the favorite keeps a name rather than a bare address.
    /// </summary>
    [RelayCommand]
    private async Task Connect()
    {
        if (_connect != null)
            await _connect(Address, Name);
    }

    /// <summary>Expands or collapses the description on card click.</summary>
    [RelayCommand(AllowConcurrentExecutions = true)]
    private async Task ToggleExpanded()
    {
        if (_cards == null)
            return;

        IsExpanded = !IsExpanded;
        _cards.SetExpanded(Address, IsExpanded);

        if (IsExpanded)
            await LoadDetails();
    }

    /// <summary>
    /// Requests the description and links from the server.
    /// </summary>
    private async Task LoadDetails()
    {
        if (_cards == null || _detailsLoaded || IsLoadingDetails)
            return;

        IsLoadingDetails = true;
        SetDetailsError(null);

        try
        {
            var info = await _cards.Info.Get(Address);

            Description = info.Description?.Trim();

            Links.Clear();
            foreach (var link in info.Links ?? [])
            {
                if (string.IsNullOrWhiteSpace(link.Name) || !WebLink.TryParse(link.Url, out var url))
                {
                    Log.Debug("Skipped link of server {Address}: {Name} → {Url}", Address, link.Name, link.Url);
                    continue;
                }

                Links.Add(new ServerLinkViewModel(link.Name.Trim(), url, LinkIcons.Find(link.Icon)));
            }

            _detailsLoaded = true;
        }
        catch (LocalizedException e)
        {
            SetDetailsError(e);
        }
        catch (Exception e)
        {
            Log.Warning(e, "Failed to fetch server info {Address}", Address);
            SetDetailsError(e);
        }
        finally
        {
            IsLoadingDetails = false;
            OnPropertyChanged(nameof(HasLinks));
            OnPropertyChanged(nameof(IsDetailsEmpty));
        }
    }

    private void SetDetailsError(Exception? error)
    {
        _detailsError = error;
        OnPropertyChanged(nameof(DetailsErrorText));
        OnPropertyChanged(nameof(HasDetailsError));
    }

    partial void OnDescriptionChanged(string? value) => OnPropertyChanged(nameof(HasDescription));

    [RelayCommand]
    private void ToggleFavorite()
    {
        _favorites.Toggle(Address, _server?.Status.Name ?? _fallbackName);
        OnPropertyChanged(nameof(IsFavorite));
    }

    protected override void OnLanguageChanged()
    {
        OnPropertyChanged(nameof(SlotsText));
        OnPropertyChanged(nameof(RoundText));
        OnPropertyChanged(nameof(SourceText));
        OnPropertyChanged(nameof(DetailsErrorText));
    }

    private static string FormatRound(MergedServer server)
    {
        if (server.Status.RunLevel == GameRunLevel.PreRoundLobby)
            return Loc.T("server-round-lobby");

        if (server.Status.RunLevel == GameRunLevel.PostRound)
            return Loc.T("server-round-ending");

        if (server.RoundDuration(DateTimeOffset.UtcNow) is not { } duration)
            return string.Empty;

        var hours = (int)duration.TotalHours;

        return hours > 0
            ? Loc.T("server-round-hours", ("hours", hours), ("minutes", duration.Minutes))
            : Loc.T("server-round-minutes", ("minutes", duration.Minutes));
    }
}
