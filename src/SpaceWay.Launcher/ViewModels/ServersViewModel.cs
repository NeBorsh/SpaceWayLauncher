using System.Collections.ObjectModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Serilog;
using SpaceWay.Core.Data;
using SpaceWay.Core.Favorites;
using SpaceWay.Core.Hubs;
using SpaceWay.Core.Localization;

namespace SpaceWay.Launcher.ViewModels;

public sealed partial class ServersViewModel : LocalizedViewModel
{
    /// <summary>
    /// How many cards to build per pass.
    /// </summary>
    private const int CardsPerBatch = 15;

    /// <summary>
    /// Rebuild generation. A pending batch that sees a different generation
    /// stops silently: a new list or sort order arrived and its cards are no longer needed.
    /// </summary>
    private int _rebuildGeneration;

    /// <summary>All servers of the current rebuild in sort order.</summary>
    private List<MergedServer> _ordered = [];

    private readonly ServerDirectory _directory;
    private readonly HubManager _hubs;
    private readonly SettingsStore _settings;
    private readonly FavoritesService _favorites;
    private readonly Func<string, string, Task>? _connect;
    private readonly ServerCardState? _cards;

    private CancellationTokenSource? _inFlight;
    private bool _loadingSettings;
    private bool _refreshingTags;

    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private int _failedHubCount;
    [ObservableProperty] private string _search = string.Empty;
    [ObservableProperty] private bool _hideFull;
    [ObservableProperty] private bool _hideEmpty;
    [ObservableProperty] private bool _hideAdultOnly;
    [ObservableProperty] private SortOption _selectedSort;
    [ObservableProperty] private bool _isTagPanelOpen;

    public ServersViewModel(
        ServerDirectory directory,
        HubManager hubs,
        SettingsStore settings,
        FavoritesService favorites,
        Func<string, string, Task>? connect = null,
        ServerCardState? cards = null)
    {
        _directory = directory;
        _hubs = hubs;
        _settings = settings;
        _favorites = favorites;
        _connect = connect;
        _cards = cards;

        _selectedSort = SortOptions[0];

        LanguageTags = new TagGroup(
            "filter-language", s => s.Languages, TagTitles.None, null, OnTagsChanged);
        RolePlayTags = new TagGroup(
            "filter-rp", s => s.RolePlayLevels, TagTitles.RolePlay, TagTitles.RolePlayOrder, OnTagsChanged);
        RegionTags = new TagGroup(
            "filter-region", s => s.Regions, TagTitles.Regions, null, OnTagsChanged);

        TagGroups = [LanguageTags, RolePlayTags, RegionTags];

        LoadSettings();
    }

    /// <summary>
    /// All servers in sort order, both those passing the filters and those
    /// hidden by them (<see cref="ServerItemViewModel.IsShown"/>).
    /// </summary>
    public ObservableCollection<ServerItemViewModel> Servers { get; } = [];

    /// <summary>Number of cards passing the filters.</summary>
    public int ShownCount { get; private set; }

    public IReadOnlyList<SortOption> SortOptions { get; } =
    [
        new(ServerSort.Players, "sort-players"),
        new(ServerSort.Occupancy, "sort-occupancy"),
        new(ServerSort.Name, "sort-name"),
        new(ServerSort.RoundTime, "sort-round-time"),
    ];

    public TagGroup LanguageTags { get; }

    public TagGroup RolePlayTags { get; }

    public TagGroup RegionTags { get; }

    public IReadOnlyList<TagGroup> TagGroups { get; }

    public int ActiveTagCount => TagGroups.Sum(g => g.Selected.Count);

    /// <summary>The tag panel toggle shows how many tags are selected, so hidden filters are never forgotten.</summary>
    public string TagsButtonText => ActiveTagCount > 0
        ? Loc.T("filter-tags-active", ("count", ActiveTagCount))
        : Loc.T("filter-tags");

    /// <summary>Number of servers received from hubs before filtering.</summary>
    public int TotalCount => _directory.Servers.Count;

    /// <summary>Number of servers hidden by filters.</summary>
    public int HiddenCount => Math.Max(0, TotalCount - ShownCount);

    /// <summary>
    /// Counter mentioning hidden servers, otherwise an empty filtered list
    /// looks like a broken hub.
    /// </summary>
    public string CountText => HiddenCount > 0
        ? Loc.T("servers-count-filtered", ("count", ShownCount), ("hidden", HiddenCount))
        : Loc.T("servers-count", ("count", ShownCount));

    /// <summary>The list is empty only because of filters; servers exist.</summary>
    public bool IsEmptyByFilter => !IsLoading && ShownCount == 0 && TotalCount > 0;

    public string EmptyByFilterText => Loc.T("servers-empty-filtered", ("hidden", HiddenCount));

    public string FailuresText => Loc.T("servers-hub-failed", ("count", FailedHubCount));

    public bool HasFailures => FailedHubCount > 0;

    /// <summary>No servers at all, before or after filtering.</summary>
    public bool IsEmpty => !IsLoading && ShownCount == 0 && TotalCount == 0;

    [RelayCommand]
    public async Task RefreshAsync()
    {
        var cts = new CancellationTokenSource();
        var previous = Interlocked.Exchange(ref _inFlight, cts);
        if (previous != null)
        {
            await previous.CancelAsync();
            previous.Dispose();
        }

        IsLoading = true;
        NotifyCountsChanged();

        try
        {
            await _directory.RefreshAsync(_hubs.Enabled, cts.Token);

            FailedHubCount = _directory.Failures.Count;
            RefreshTagOptions();
            Rebuild();
        }
        catch (OperationCanceledException)
        {
            return;
        }
        catch (Exception e)
        {
            Log.Error(e, "Failed to fetch server list");
            FailedHubCount = _hubs.Enabled.Count;
        }
        finally
        {
            IsLoading = false;
            NotifyCountsChanged();
        }
    }

    [RelayCommand]
    public void ResetFilters()
    {
        Search = string.Empty;
        HideFull = false;
        HideEmpty = false;
        HideAdultOnly = false;
        SelectedSort = SortOptions[0];

        _refreshingTags = true;

        try
        {
            foreach (var group in TagGroups)
                group.Clear();
        }
        finally
        {
            _refreshingTags = false;
        }

        OnTagsChanged();
    }

    /// <summary>Recreates cards: new hub list or new sort order.</summary>
    private void Rebuild()
    {
        var generation = ++_rebuildGeneration;
        var filter = CurrentFilter();

        _ordered = filter.Order(_directory.Servers).ToList();
        Servers.Clear();

        ApplyFilter(filter);

        AddBatch(generation, 0);
    }

    private void AddBatch(int generation, int start)
    {
        if (generation != _rebuildGeneration)
            return;

        var filter = CurrentFilter();
        var end = Math.Min(start + CardsPerBatch, _ordered.Count);

        for (var i = start; i < end; i++)
        {
            var server = _ordered[i];
            Servers.Add(new ServerItemViewModel(server, _favorites, _connect, _cards)
            {
                IsShown = filter.Matches(server),
            });
        }

        if (end < _ordered.Count)
            Dispatcher.UIThread.Post(() => AddBatch(generation, end), DispatcherPriority.Background);
    }

    /// <summary>Hides and shows existing cards without recreating them.</summary>
    private void ApplyFilter() => ApplyFilter(CurrentFilter());

    private void ApplyFilter(ServerFilter filter)
    {
        foreach (var item in Servers)
            item.IsShown = item.Server is { } server && filter.Matches(server);

        ShownCount = _ordered.Count(filter.Matches);
        NotifyCountsChanged();
    }

    private ServerFilter CurrentFilter()
    {
        return new ServerFilter
        {
            Search = Search,
            HideFull = HideFull,
            HideEmpty = HideEmpty,
            HideAdultOnly = HideAdultOnly,
            Sort = SelectedSort.Sort,
            Languages = LanguageTags.Selected,
            RolePlayLevels = RolePlayTags.Selected,
            Regions = RegionTags.Selected,
        };
    }

    private void NotifyCountsChanged()
    {
        OnPropertyChanged(nameof(CountText));
        OnPropertyChanged(nameof(ShownCount));
        OnPropertyChanged(nameof(TotalCount));
        OnPropertyChanged(nameof(HiddenCount));
        OnPropertyChanged(nameof(IsEmpty));
        OnPropertyChanged(nameof(IsEmptyByFilter));
        OnPropertyChanged(nameof(EmptyByFilterText));
    }

    private void RefreshTagOptions()
    {
        _refreshingTags = true;

        try
        {
            var servers = _directory.Servers;

            LanguageTags.Refresh(servers, KeepSelected(LanguageTags, SettingKeys.FilterLanguages));
            RolePlayTags.Refresh(servers, KeepSelected(RolePlayTags, SettingKeys.FilterRolePlay));
            RegionTags.Refresh(servers, KeepSelected(RegionTags, SettingKeys.FilterRegions));
        }
        finally
        {
            _refreshingTags = false;
        }

        NotifyTagsChanged();
    }

    /// <summary>The current selection, or the saved one before the first list arrives.</summary>
    private IReadOnlyCollection<string> KeepSelected(TagGroup group, string key) =>
        group.HasChips ? group.Selected : ReadList(key);

    private IReadOnlyList<string> ReadList(string key) =>
        _settings.Get(key)?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) ?? [];

    private void OnTagsChanged()
    {
        if (_refreshingTags)
            return;

        ApplyFilter();
        SaveSettings();
        NotifyTagsChanged();
    }

    private void NotifyTagsChanged()
    {
        OnPropertyChanged(nameof(ActiveTagCount));
        OnPropertyChanged(nameof(TagsButtonText));
    }

    [RelayCommand]
    private void ToggleTagPanel() => IsTagPanelOpen = !IsTagPanelOpen;

    private void LoadSettings()
    {
        _loadingSettings = true;

        try
        {
            HideFull = _settings.GetBool(SettingKeys.FilterHideFull, false);
            HideEmpty = _settings.GetBool(SettingKeys.FilterHideEmpty, false);
            HideAdultOnly = _settings.GetBool(SettingKeys.FilterHideAdult, false);

            var sort = _settings.Get(SettingKeys.FilterSort);
            if (Enum.TryParse<ServerSort>(sort, out var parsed))
                SelectedSort = SortOptions.FirstOrDefault(o => o.Sort == parsed) ?? SortOptions[0];
        }
        finally
        {
            _loadingSettings = false;
        }
    }

    private void SaveSettings()
    {
        if (_loadingSettings)
            return;

        _settings.SetBool(SettingKeys.FilterHideFull, HideFull);
        _settings.SetBool(SettingKeys.FilterHideEmpty, HideEmpty);
        _settings.SetBool(SettingKeys.FilterHideAdult, HideAdultOnly);
        _settings.Set(SettingKeys.FilterSort, SelectedSort.Sort.ToString());

        if (LanguageTags.HasChips)
            _settings.Set(SettingKeys.FilterLanguages, string.Join(',', LanguageTags.Selected));

        if (RolePlayTags.HasChips)
            _settings.Set(SettingKeys.FilterRolePlay, string.Join(',', RolePlayTags.Selected));

        if (RegionTags.HasChips)
            _settings.Set(SettingKeys.FilterRegions, string.Join(',', RegionTags.Selected));
    }

    partial void OnSearchChanged(string value) => ApplyFilter();

    partial void OnHideFullChanged(bool value) => OnFilterChanged();

    partial void OnHideEmptyChanged(bool value) => OnFilterChanged();

    partial void OnHideAdultOnlyChanged(bool value) => OnFilterChanged();

    partial void OnSelectedSortChanged(SortOption value)
    {
        Rebuild();
        SaveSettings();
    }

    private void OnFilterChanged()
    {
        ApplyFilter();
        SaveSettings();
    }

    partial void OnFailedHubCountChanged(int value)
    {
        OnPropertyChanged(nameof(FailuresText));
        OnPropertyChanged(nameof(HasFailures));
    }

    protected override void OnLanguageChanged()
    {
        OnPropertyChanged(nameof(CountText));
        OnPropertyChanged(nameof(FailuresText));
        OnPropertyChanged(nameof(TagsButtonText));

        foreach (var group in TagGroups)
            group.RefreshTitles();
    }
}

public sealed record SortOption(ServerSort Sort, string TitleKey)
{
    public string Title => Loc.T(TitleKey);
}
