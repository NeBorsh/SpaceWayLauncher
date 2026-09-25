using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Platform.Storage;
using Serilog;
using SpaceWay.Launcher.ViewModels;

namespace SpaceWay.Launcher.Views;

/// <summary>
/// Mods section. The code-behind exists only for drag and drop: drop events
/// do not fit into bindings, and the view model decides what to do with files.
/// </summary>
public sealed partial class ModsView : UserControl
{
    public ModsView()
    {
        InitializeComponent();

        AddHandler(DragDrop.DragEnterEvent, OnDragOver);
        AddHandler(DragDrop.DragOverEvent, OnDragOver);
        AddHandler(DragDrop.DragLeaveEvent, OnDragLeave);
        AddHandler(DragDrop.DropEvent, OnDrop);
    }

    private void OnDragOver(object? sender, DragEventArgs e)
    {
        if (!HasAssemblies(e))
        {
            DropOverlay.IsVisible = false;
            return;
        }

        e.DragEffects = DragDropEffects.Copy;
        e.Handled = true;
        DropOverlay.IsVisible = true;
    }

    private static bool HasAssemblies(DragEventArgs e) =>
        e.DataTransfer.TryGetFiles() is { } items
        && items.Any(i => i.Name.EndsWith(".dll", StringComparison.OrdinalIgnoreCase));

    private void OnDragLeave(object? sender, DragEventArgs e) => DropOverlay.IsVisible = false;

    private async void OnDrop(object? sender, DragEventArgs e)
    {
        DropOverlay.IsVisible = false;

        if (!HasAssemblies(e) || DataContext is not ModsViewModel model
            || e.DataTransfer.TryGetFiles() is not { } items)
            return;

        e.Handled = true;

        var paths = items
            .Select(item => item.TryGetLocalPath())
            .OfType<string>()
            .ToList();

        if (paths.Count == 0)
            return;

        try
        {
            await model.ImportAsync(paths);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to add dropped files");
            model.ErrorText = ex.Message;
        }
    }
}
