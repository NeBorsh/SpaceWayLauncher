using CommunityToolkit.Mvvm.ComponentModel;
using SpaceWay.Core.Localization;

namespace SpaceWay.Launcher.ViewModels;

/// <summary>
/// Base for view models with strings built in code (e.g. with pluralization,
/// which <c>{loc:Translate}</c> cannot do). Such strings must be rebuilt
/// when the language changes.
/// </summary>
public abstract class LocalizedViewModel : ObservableObject, IDisposable
{
    /// <summary>
    /// The delegate is kept in a field on purpose: <see cref="Loc"/> holds only
    /// a weak reference to it, and without the field it would be collected immediately.
    /// </summary>
    private readonly Action _onLanguageChanged;

    protected LocalizedViewModel()
    {
        _onLanguageChanged = OnLanguageChanged;
        Loc.SubscribeWeak(_onLanguageChanged);
    }

    /// <summary>
    /// Called when the language changes. Derived classes raise
    /// <see cref="ObservableObject.OnPropertyChanged(string?)"/> for their strings here.
    /// </summary>
    protected abstract void OnLanguageChanged();

    /// <summary>
    /// Unsubscribes immediately. Optional, since the garbage collector removes the
    /// subscription eventually, but a window that just closed need not wait for it.
    /// </summary>
    public void Dispose() => Loc.UnsubscribeWeak(_onLanguageChanged);
}
