// Based on SS14.Launcher, Copyright (c) 2019 Space Station 14 Contributors. MIT License, see NOTICE.

using Avalonia;
using Avalonia.Controls.Shapes;
using Avalonia.Media;

namespace SpaceWay.Launcher.Controls;

/// <summary>
/// Rectangle with cut corners, the signature SS14 UI shape.
/// </summary>
public class AngleBox : Shape
{
    public static readonly StyledProperty<double> CornerSizeProperty =
        AvaloniaProperty.Register<AngleBox, double>(nameof(CornerSize), 6d);

    public static readonly StyledProperty<AngleBoxSides> SidesProperty =
        AvaloniaProperty.Register<AngleBox, AngleBoxSides>(nameof(Sides));

    static AngleBox()
    {
        AffectsGeometry<AngleBox>(BoundsProperty, CornerSizeProperty, SidesProperty);
    }

    /// <summary>Corner cut length in pixels.</summary>
    public double CornerSize
    {
        get => GetValue(CornerSizeProperty);
        set => SetValue(CornerSizeProperty, value);
    }

    /// <summary>Sides to keep straight.</summary>
    public AngleBoxSides Sides
    {
        get => GetValue(SidesProperty);
        set => SetValue(SidesProperty, value);
    }

    /// <summary>
    /// The shape does not request a size: the parent panel stretches it,
    /// and geometry is rebuilt from the final Bounds.
    /// </summary>
    protected override Size MeasureOverride(Size availableSize)
    {
        var corner = CornerSize;
        return new Size(corner, corner);
    }

    protected override Geometry CreateDefiningGeometry()
    {
        var corner = CornerSize;
        var width = Bounds.Width;
        var height = Bounds.Height;
        var sides = Sides;

        corner = Math.Min(corner, Math.Min(width, height) / 2);

        var geometry = new StreamGeometry();
        using var context = geometry.Open();

        context.BeginFigure(new Point(0, 0), isFilled: true);

        if (sides.HasFlag(AngleBoxSides.OpenRight))
        {
            context.LineTo(new Point(width, 0));
        }
        else
        {
            context.LineTo(new Point(width - corner, 0));
            context.LineTo(new Point(width, corner));
        }

        context.LineTo(new Point(width, height));

        if (sides.HasFlag(AngleBoxSides.OpenLeft))
        {
            context.LineTo(new Point(0, height));
        }
        else
        {
            context.LineTo(new Point(corner, height));
            context.LineTo(new Point(0, height - corner));
        }

        context.EndFigure(isClosed: true);

        return geometry;
    }
}

/// <summary>Sides without a corner cut, for placing elements flush against each other.</summary>
[Flags]
public enum AngleBoxSides
{
    None = 0,
    OpenLeft = 1,
    OpenRight = 2,
}
