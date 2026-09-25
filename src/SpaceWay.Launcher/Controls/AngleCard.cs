using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;

namespace SpaceWay.Launcher.Controls;

/// <summary>
/// Container with cut corners, used instead of Border where the signature
/// shape is needed. The template is defined in Theme/Styles.axaml.
/// </summary>
public class AngleCard : ContentControl
{
    public static readonly StyledProperty<double> CornerSizeProperty =
        AvaloniaProperty.Register<AngleCard, double>(nameof(CornerSize), 8d);

    public static readonly StyledProperty<ICommand?> CommandProperty =
        AvaloniaProperty.Register<AngleCard, ICommand?>(nameof(Command));

    public double CornerSize
    {
        get => GetValue(CornerSizeProperty);
        set => SetValue(CornerSizeProperty, value);
    }

    /// <summary>
    /// Command invoked when the card itself is clicked.
    /// </summary>
    public ICommand? Command
    {
        get => GetValue(CommandProperty);
        set => SetValue(CommandProperty, value);
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);

        if (e.Handled || e.InitialPressMouseButton != MouseButton.Left || Command is not { } command)
            return;

        if (!new Rect(Bounds.Size).Contains(e.GetPosition(this)))
            return;

        if (command.CanExecute(null))
        {
            command.Execute(null);
            e.Handled = true;
        }
    }
}
