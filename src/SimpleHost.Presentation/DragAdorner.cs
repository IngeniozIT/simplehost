using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;

// This project enables WinForms interop (for the tray icon), whose implicit usings bring
// System.Drawing into scope — making the unqualified Point, Size and Brush below ambiguous with
// System.Drawing's same-named types. Pin them to their WPF counterparts, which is what this
// adorner uses throughout.
using Point = System.Windows.Point;
using Size = System.Windows.Size;
using Brush = System.Windows.Media.Brush;
using Image = System.Windows.Controls.Image;
using Color = System.Windows.Media.Color;

namespace SimpleHost.Presentation;

/// <summary>
/// A translucent "ghost" of the entry row being dragged, drawn on the window's adorner layer so it
/// floats above the entries list and follows the mouse cursor during a drag-reorder. It is purely
/// visual — never hit-testable — and paints a one-off snapshot of the row taken when the drag began,
/// so dimming the real row underneath does not dim the ghost. The owning window moves it by calling
/// <see cref="SetOffset"/> from its drag-over handler; there is no data or view-model coupling here,
/// keeping the reorder/move staging (which stays in the view-model) entirely unchanged.
/// </summary>
internal sealed class DragAdorner : Adorner
{
    private readonly VisualCollection _children;
    private readonly Border _card;
    private readonly Size _size;
    private Point _offset;

    public DragAdorner(UIElement adornedElement, FrameworkElement draggedRow)
        : base(adornedElement)
    {
        ArgumentNullException.ThrowIfNull(adornedElement);
        ArgumentNullException.ThrowIfNull(draggedRow);

        _size = draggedRow.RenderSize;

        // Snapshot the row into a bitmap so the ghost is a frozen copy: it must not track the live
        // row, which we dim to 0.35 opacity while the drag is in flight.
        var dpi = VisualTreeHelper.GetDpi(draggedRow);
        var snapshot = new RenderTargetBitmap(
            Math.Max(1, (int)Math.Ceiling(_size.Width * dpi.DpiScaleX)),
            Math.Max(1, (int)Math.Ceiling(_size.Height * dpi.DpiScaleY)),
            dpi.PixelsPerInchX,
            dpi.PixelsPerInchY,
            PixelFormats.Pbgra32);
        snapshot.Render(draggedRow);

        var ghost = new Image
        {
            Source = snapshot,
            Width = _size.Width,
            Height = _size.Height,
            Stretch = Stretch.Fill,
        };

        _card = new Border
        {
            Width = _size.Width,
            Height = _size.Height,
            CornerRadius = new CornerRadius(6),
            Background = ResolveBackground(adornedElement),
            Opacity = 0.85,
            Child = ghost,
            IsHitTestVisible = false,
            Effect = new DropShadowEffect
            {
                BlurRadius = 14,
                ShadowDepth = 2,
                Direction = 270,
                Opacity = 0.45,
            },
        };

        IsHitTestVisible = false;
        _children = new VisualCollection(this) { _card };
    }

    /// <summary>Positions the ghost's top-left corner, in the adorned element's coordinate space.</summary>
    public void SetOffset(Point offset)
    {
        _offset = offset;
        // Nudge the adorner layer to re-arrange this adorner at its new offset.
        (Parent as AdornerLayer)?.Update(AdornedElement);
        InvalidateArrange();
    }

    protected override int VisualChildrenCount => _children.Count;

    protected override Visual GetVisualChild(int index) => _children[index];

    protected override Size MeasureOverride(Size constraint)
    {
        _card.Measure(_size);
        return _size;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        _card.Arrange(new Rect(_offset, _size));
        return finalSize;
    }

    private static Brush ResolveBackground(UIElement adorned)
    {
        // Prefer the current Fluent theme's window background so the ghost reads as a solid card in
        // both light and dark themes; fall back to a neutral grey if the resource is unavailable.
        if (adorned is FrameworkElement element
            && element.TryFindResource("ApplicationBackgroundBrush") is Brush themed)
        {
            return themed;
        }

        return new SolidColorBrush(Color.FromRgb(0x2B, 0x2B, 0x2B));
    }
}
