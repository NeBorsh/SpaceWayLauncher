using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Serilog;
using SpaceWay.Core.Localization;
using SpaceWay.Launcher.ViewModels;

namespace SpaceWay.Launcher.Views;

/// <summary>
/// Main window. The code-behind handles file picking and replay drag and drop,
/// neither of which fits into bindings.
/// </summary>
public sealed partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        AddHandler(DragDrop.DragEnterEvent, OnDragOver);
        AddHandler(DragDrop.DragOverEvent, OnDragOver);
        AddHandler(DragDrop.DragLeaveEvent, OnDragLeave);
        AddHandler(DragDrop.DropEvent, OnDrop);
    }

    private MainWindowViewModel? Model => DataContext as MainWindowViewModel;

    private async void OpenBundleClicked(object? sender, RoutedEventArgs e)
    {
        try
        {
            var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = Loc.T("bundle-picker-title"),
                AllowMultiple = false,
                FileTypeFilter =
                [
                    new FilePickerFileType(Loc.T("bundle-picker-type"))
                    {
                        Patterns = ["*.zip"],
                        MimeTypes = ["application/zip"],
                        AppleUniformTypeIdentifiers = ["public.zip-archive"],
                    },
                ],
            });

            if (files.Count > 0 && files[0].TryGetLocalPath() is { } path && Model is { } model)
                await model.OpenBundleAsync(path);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to open replay");
        }
    }

    private void OnDragOver(object? sender, DragEventArgs e)
    {
        var accepted = BundleIn(e) != null;

        e.DragEffects = accepted ? DragDropEffects.Copy : DragDropEffects.None;
        BundleDropOverlay.IsVisible = accepted;
    }

    private void OnDragLeave(object? sender, DragEventArgs e) => BundleDropOverlay.IsVisible = false;

    private async void OnDrop(object? sender, DragEventArgs e)
    {
        BundleDropOverlay.IsVisible = false;

        if (BundleIn(e) is not { } path || Model is not { } model)
            return;

        try
        {
            await model.OpenBundleAsync(path);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to launch dropped replay");
        }
    }

    /// <summary>
    /// Path to a dropped replay, or null.
    /// </summary>
    private string? BundleIn(DragEventArgs e)
    {
        if (Model is not { } model || model.Dialogs.IsOpen)
            return null;

        if (e.DataTransfer.TryGetFiles() is not { } items)
            return null;

        var paths = items.Select(i => i.TryGetLocalPath()).OfType<string>().ToList();

        return paths.Count == 1 && paths[0].EndsWith(".zip", StringComparison.OrdinalIgnoreCase)
            ? paths[0]
            : null;
    }
}
