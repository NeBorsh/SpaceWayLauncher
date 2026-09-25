using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using SpaceWay.Core.Hubs;
using SpaceWay.Core.Localization;

namespace SpaceWay.Launcher.ViewModels;

/// <summary>
/// One row of tag filters: languages, role-play levels or regions. The values come from
/// the servers actually received, so the row never offers an option that matches nothing.
/// </summary>
public sealed class TagGroup(
    string titleKey,
    Func<MergedServer, IReadOnlyList<string>> values,
    IReadOnlyDictionary<string, string> titleKeys,
    IReadOnlyList<string>? fixedOrder,
    Action changed) : ObservableObject
{
    public string Title => Loc.T(titleKey);

    public ObservableCollection<TagChip> Chips { get; } = [];

    public bool HasChips => Chips.Count > 0;

    public IReadOnlyList<string> Selected => [.. Chips.Where(c => c.IsSelected).Select(c => c.Code)];

    /// <summary>Rebuilds the chips for a new server list, keeping the given codes selected.</summary>
    public void Refresh(IReadOnlyCollection<MergedServer> servers, IReadOnlyCollection<string> keepSelected)
    {
        var counts = servers
            .SelectMany(values)
            .GroupBy(v => v)
            .ToDictionary(g => g.Key, g => g.Count());

        foreach (var code in keepSelected)
            counts.TryAdd(code, 0);

        var ordered = counts.Keys
            .OrderBy(OrderIndex)
            .ThenByDescending(code => counts[code])
            .ThenBy(code => code, StringComparer.Ordinal);

        Chips.Clear();

        foreach (var code in ordered)
        {
            Chips.Add(new TagChip(code, TitleOf(code), counts[code], changed)
            {
                IsSelected = keepSelected.Contains(code),
            });
        }

        OnPropertyChanged(nameof(HasChips));
    }

    public void Clear()
    {
        foreach (var chip in Chips)
            chip.IsSelected = false;
    }

    public void RefreshTitles()
    {
        OnPropertyChanged(nameof(Title));

        foreach (var chip in Chips)
            chip.Title = TitleOf(chip.Code);
    }

    private int OrderIndex(string code)
    {
        for (var i = 0; i < (fixedOrder?.Count ?? 0); i++)
        {
            if (fixedOrder![i] == code)
                return i;
        }

        return int.MaxValue;
    }

    private string TitleOf(string code) =>
        titleKeys.TryGetValue(code, out var key) ? Loc.T(key) : code.ToUpperInvariant();
}

public sealed partial class TagChip : ObservableObject
{
    private readonly Action _changed;

    [ObservableProperty] private bool _isSelected;
    [ObservableProperty] private string _title;

    public TagChip(string code, string title, int count, Action changed)
    {
        Code = code;
        Count = count;
        _title = title;
        _changed = changed;
    }

    public string Code { get; }

    public int Count { get; }

    partial void OnIsSelectedChanged(bool value) => _changed();
}

/// <summary>Human-readable names for known tag values; anything else is shown as its code.</summary>
public static class TagTitles
{
    public static IReadOnlyList<string> RolePlayOrder { get; } = ["none", "low", "med", "high"];

    public static IReadOnlyDictionary<string, string> RolePlay { get; } = new Dictionary<string, string>
    {
        ["none"] = "tag-rp-none",
        ["low"] = "tag-rp-low",
        ["med"] = "tag-rp-med",
        ["high"] = "tag-rp-high",
    };

    public static IReadOnlyDictionary<string, string> Regions { get; } = new Dictionary<string, string>
    {
        ["eu_e"] = "tag-region-eu_e",
        ["eu_w"] = "tag-region-eu_w",
        ["am_n_e"] = "tag-region-am_n_e",
        ["am_n_w"] = "tag-region-am_n_w",
        ["am_n_c"] = "tag-region-am_n_c",
        ["am_c"] = "tag-region-am_c",
        ["am_s_e"] = "tag-region-am_s_e",
        ["am_s_w"] = "tag-region-am_s_w",
        ["am_s_s"] = "tag-region-am_s_s",
        ["as_e"] = "tag-region-as_e",
        ["as_n"] = "tag-region-as_n",
        ["as_se"] = "tag-region-as_se",
        ["af_n"] = "tag-region-af_n",
        ["af_c"] = "tag-region-af_c",
        ["af_s"] = "tag-region-af_s",
        ["oce"] = "tag-region-oce",
    };

    public static IReadOnlyDictionary<string, string> None { get; } = new Dictionary<string, string>();
}
