#pragma warning disable CS0618

using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
using SpaceWay.Launcher.ViewModels;

namespace SpaceWay.Launcher.Views;

/// <summary>
/// Hub order is set by dragging cards.
/// </summary>
public sealed partial class HubsView : UserControl
{
    /// <summary>Data format for drag and drop within the launcher.</summary>
    private const string DragFormat = "spaceway/hub";

    /// <summary>
    /// How far the cursor must move to count as a drag.
    /// Without a threshold any click on a card would start a drag,
    /// making the checkbox impossible to click.
    /// </summary>
    private const double DragThreshold = 6;

    private Point _pressPosition;
    private HubItemViewModel? _pressed;

    public HubsView()
    {
        InitializeComponent();

        AddHandler(PointerPressedEvent, OnPointerPressed, handledEventsToo: true);
        AddHandler(PointerMovedEvent, OnPointerMoved, handledEventsToo: true);
        AddHandler(PointerReleasedEvent, OnPointerReleased, handledEventsToo: true);
        AddHandler(DragDrop.DragOverEvent, OnDragOver);
        AddHandler(DragDrop.DropEvent, OnDrop);
    }

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            return;

        _pressPosition = e.GetPosition(this);
        _pressed = FindItem(e.Source as Visual);
    }

    private async void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        if (_pressed is not { } dragged)
            return;

        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            _pressed = null;
            return;
        }

        var delta = e.GetPosition(this) - _pressPosition;
        if (Math.Abs(delta.X) < DragThreshold && Math.Abs(delta.Y) < DragThreshold)
            return;

        _pressed = null;

        var data = new DataObject();
        data.Set(DragFormat, dragged);

        await DragDrop.DoDragDrop(e, data, DragDropEffects.Move);
    }

    private void OnPointerReleased(object? sender, PointerReleasedEventArgs e) => _pressed = null;

    private void OnDragOver(object? sender, DragEventArgs e)
    {
        var dragged = e.Data.Get(DragFormat) as HubItemViewModel;
        var target = FindItem(e.Source as Visual);

        e.DragEffects = dragged != null && target != null && !ReferenceEquals(dragged, target)
            ? DragDropEffects.Move
            : DragDropEffects.None;

        Highlight(target, dragged);
        e.Handled = true;
    }

    private void OnDrop(object? sender, DragEventArgs e)
    {
        ClearHighlight();

        if (DataContext is not HubsViewModel viewModel)
            return;

        if (e.Data.Get(DragFormat) is not HubItemViewModel dragged)
            return;

        if (FindItem(e.Source as Visual) is not { } target || ReferenceEquals(dragged, target))
            return;

        viewModel.Reorder(dragged, target);
        e.Handled = true;
    }

    /// <summary>Finds the hub card owning the element under the cursor.</summary>
    private static HubItemViewModel? FindItem(Visual? source) =>
        source?.FindAncestorOfType<ListBoxItem>(includeSelf: true)?.DataContext as HubItemViewModel
        ?? (source?.DataContext as HubItemViewModel);

    private void Highlight(HubItemViewModel? target, HubItemViewModel? dragged)
    {
        if (DataContext is not HubsViewModel viewModel)
            return;

        foreach (var item in viewModel.Items)
        {
            item.IsDropTarget = ReferenceEquals(item, target) && !ReferenceEquals(item, dragged);
        }
    }

    private void ClearHighlight()
    {
        if (DataContext is not HubsViewModel viewModel)
            return;

        foreach (var item in viewModel.Items)
        {
            item.IsDropTarget = false;
        }
    }
}

#pragma warning restore CS0618
