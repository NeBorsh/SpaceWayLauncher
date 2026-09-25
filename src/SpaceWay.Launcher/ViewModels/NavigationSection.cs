using Avalonia.Media;
using SpaceWay.Core.Localization;

namespace SpaceWay.Launcher.ViewModels;

/// <summary>
/// Sidebar item together with its section content.
/// </summary>
public sealed class NavigationSection(string titleKey, Geometry icon, object content)
    : LocalizedViewModel
{
    public string Title => Loc.T(titleKey);

    /// <summary>
    /// Item icon. Carried by the item itself; looking it up by resource key
    /// from the list template would require a converter.
    /// </summary>
    public Geometry Icon { get; } = icon;

    public object Content { get; } = content;

    protected override void OnLanguageChanged() => OnPropertyChanged(nameof(Title));
}
