using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Serilog;
using SpaceWay.Core.Hubs;
using SpaceWay.Core.Localization;

namespace SpaceWay.Launcher.ViewModels;

public sealed partial class AddHubDialogViewModel(HubManager hubs) : DialogViewModel<bool>
{
    [ObservableProperty] private string _displayName = string.Empty;
    [ObservableProperty] private string _address = string.Empty;
    [ObservableProperty] private string? _errorText;

    public override string Title => Loc.T("hubs-add-title");

    public bool HasError => !string.IsNullOrEmpty(ErrorText);

    public bool CanSubmit => !string.IsNullOrWhiteSpace(DisplayName)
                             && Uri.TryCreate(Normalize(Address), UriKind.Absolute, out var uri)
                             && uri.Scheme is "http" or "https";

    [RelayCommand]
    private void Submit()
    {
        if (!CanSubmit)
            return;

        try
        {
            var priority = hubs.Hubs.Count == 0 ? 0 : hubs.Hubs.Max(h => h.Priority) + 1;

            hubs.Save(new HubEntry(
                Guid.NewGuid(),
                DisplayName.Trim(),
                new Uri(Normalize(Address)),
                priority));

            Close(true);
        }
        catch (Exception e)
        {
            Log.Error(e, "Failed to add hub");
            ErrorText = e.Message;
        }
    }

    /// <summary>
    /// Normalizes the input into a form usable for requests.
    /// </summary>
    private static string Normalize(string address)
    {
        var trimmed = address.Trim();

        if (trimmed.Length == 0)
            return trimmed;

        if (!trimmed.Contains("://", StringComparison.Ordinal))
            trimmed = "https://" + trimmed;

        return trimmed.EndsWith('/') ? trimmed : trimmed + "/";
    }

    partial void OnDisplayNameChanged(string value) => OnPropertyChanged(nameof(CanSubmit));

    partial void OnAddressChanged(string value) => OnPropertyChanged(nameof(CanSubmit));

    partial void OnErrorTextChanged(string? value) => OnPropertyChanged(nameof(HasError));
}
