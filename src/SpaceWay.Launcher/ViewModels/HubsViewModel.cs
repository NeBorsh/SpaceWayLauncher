using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Input;
using SpaceWay.Core.Hubs;
using SpaceWay.Core.Localization;

namespace SpaceWay.Launcher.ViewModels;

public sealed partial class HubsViewModel : LocalizedViewModel
{
    private readonly HubManager _hubs;
    private readonly DialogService _dialogs;

    public HubsViewModel(HubManager hubs, DialogService dialogs)
    {
        _hubs = hubs;
        _dialogs = dialogs;
        _hubs.Changed += Reload;

        Reload();
    }

    public ObservableCollection<HubItemViewModel> Items { get; } = [];

    public bool IsEmpty => Items.Count == 0;

    /// <summary>
    /// All hubs are disabled, so the server list will be empty and the player
    /// should know why.
    /// </summary>
    public bool AllDisabled => Items.Count > 0 && Items.All(i => !i.IsEnabled);

    [RelayCommand]
    private async Task AddHubAsync()
    {
        await _dialogs.ShowAsync(new AddHubDialogViewModel(_hubs));
    }

    /// <summary>Moves a dragged hub to another hub's position.</summary>
    public void Reorder(HubItemViewModel dragged, HubItemViewModel target)
    {
        var targetIndex = Items.IndexOf(target);
        if (targetIndex >= 0)
            _hubs.Reorder(dragged.Id, targetIndex);
    }

    protected override void OnLanguageChanged() => Reload();

    private void Reload()
    {
        Items.Clear();

        var ordered = _hubs.Hubs.OrderBy(h => h.Priority).ToList();

        for (var i = 0; i < ordered.Count; i++)
        {
            Items.Add(new HubItemViewModel(ordered[i], _hubs));
        }

        OnPropertyChanged(nameof(IsEmpty));
        OnPropertyChanged(nameof(AllDisabled));
    }
}

public sealed partial class HubItemViewModel(HubEntry hub, HubManager hubs) : LocalizedViewModel
{
    private bool _isDropTarget;

    public Guid Id => hub.Id;

    /// <summary>A dragged hub is currently being dropped here.</summary>
    public bool IsDropTarget
    {
        get => _isDropTarget;
        set => SetProperty(ref _isDropTarget, value);
    }

    public string DisplayName => hub.DisplayName;

    public string Address => hub.Address.AbsoluteUri;

    public bool IsEnabled
    {
        get => hub.Enabled;
        set
        {
            if (hub.Enabled == value)
                return;

            hubs.SetEnabled(hub.Id, value);
        }
    }

    /// <summary>
    /// Position number, which is also the priority when merging server lists.
    /// </summary>
    public string PriorityText => Loc.T("hubs-priority", ("position", hub.Priority + 1));

    protected override void OnLanguageChanged() => OnPropertyChanged(nameof(PriorityText));

    [RelayCommand]
    private void Remove() => hubs.Remove(hub.Id);
}
