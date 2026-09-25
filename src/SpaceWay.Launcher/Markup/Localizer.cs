using System.ComponentModel;
using SpaceWay.Core.Localization;

namespace SpaceWay.Launcher.Markup;

/// <summary>
/// String source for XAML bindings.
/// An indexer instead of properties, since there are hundreds of keys.
/// </summary>
public sealed class Localizer : INotifyPropertyChanged
{
    public static Localizer Instance { get; } = new();

    private Localizer()
    {
        Loc.LanguageChanged += () =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));
    }

    public string this[string key] => Loc.T(key);

    public event PropertyChangedEventHandler? PropertyChanged;
}
