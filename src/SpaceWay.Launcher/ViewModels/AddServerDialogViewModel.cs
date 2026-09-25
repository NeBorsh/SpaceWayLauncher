using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SpaceWay.Core.Favorites;
using SpaceWay.Core.Hubs;
using SpaceWay.Core.Localization;

namespace SpaceWay.Launcher.ViewModels;

/// <summary>
/// Manually adding a server to favorites.
/// </summary>
public sealed partial class AddServerDialogViewModel(FavoritesService favorites)
    : DialogViewModel<bool>
{
    [ObservableProperty] private string _displayName = string.Empty;
    [ObservableProperty] private string _address = string.Empty;
    [ObservableProperty] private string? _errorText;

    public override string Title => Loc.T("favorites-add-title");

    public bool HasError => !string.IsNullOrEmpty(ErrorText);

    /// <summary>
    /// The name is optional: an address alone is a valid entry and can be
    /// labeled later. Only a parseable address is required.
    /// </summary>
    public bool CanSubmit => ServerAddress.TryParse(Address, out _);

    [RelayCommand]
    private void Submit()
    {
        if (!ServerAddress.TryParse(Address, out var uri))
        {
            ErrorText = Loc.T("favorites-add-bad-address");
            return;
        }

        if (!favorites.Add(uri.AbsoluteUri, DisplayName))
        {
            ErrorText = Loc.T("favorites-add-duplicate");
            return;
        }

        Close(true);
    }

    partial void OnAddressChanged(string value)
    {
        OnPropertyChanged(nameof(CanSubmit));

        ErrorText = null;
    }

    partial void OnErrorTextChanged(string? value) => OnPropertyChanged(nameof(HasError));
}
