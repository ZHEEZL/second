using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using RecursiveLab.Core.Geometry;
using RecursiveLab.Core.Tasks.ClosestPair;

namespace RecursiveLab.App.Controls;

/// <summary>
/// Рисует облако точек, полосу и найденную ближайшую пару.
/// Алгоритм в Core только сообщает результат/шаги — контрол занимается картинкой.
/// </summary>
public sealed class PointsCanvas : Control
{
    private static readonly IBrush PointBrush = new SolidColorBrush(Color.Parse("#67E8F9"));
    private static readonly IBrush PairBrush = new SolidColorBrush(Color.Parse("#FBBF24"));
    private static readonly IBrush StripBrush = new SolidColorBrush(Color.Parse("#1622D3EE"));
    private static readonly IBrush BorderBrush = new SolidColorBrush(Color.Parse("#223152"));
    private static readonly IBrush AreaBrush = new SolidColorBrush(Color.Parse("#0B1425"));
    private static readonly IBrush GridBrush = new SolidColorBrush(Color.FromArgb(10, 199, 214, 255));
    private static readonly IBrush StripLineBrush = new SolidColorBrush(Color.FromArgb(120, 34, 211, 238));

    private IReadOnlyList<PointD> _points = Array.Empty<PointD>();
    private ClosestPairResult? _bestPair;
    private (double X0, double X1)? _strip;
    private double? _splitX;

    public PointsCanvas()
    {
        ClipToBounds = true;
    }

    public IReadOnlyList<PointD> Points
    {
        get => _points;
        set
        {
            _points = value ?? Array.Empty<PointD>();
            InvalidateVisual();
        }
    }

    public ClosestPairResult? BestPair
    {
        get => _bestPair;
        set
        {
            _bestPair = value;
            InvalidateVisual();
        }
    }

    public (double X0, double X1)? Strip
    {
        get => _strip;
        set
        {
            _strip = value;
            InvalidateVisual();
        }
    }

    public double? SplitX
    {
        get => _splitX;
        set
        {
            _splitX = value;
            InvalidateVisual();
        }
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        var width = Bounds.Width;
        var height = Bounds.Height;

        if (width <= 1 || height <= 1)
        {
            return;
        }

        var size = Math.Min(width, height) - 48;
        var left = (width - size) / 2;
        var top = (height - size) / 2;
        var area = new Rect(left, top, size, size);

        context.DrawRectangle(AreaBrush, null, new RoundedRect(area, 14));

        DrawGrid(context, area);

        if (_strip is { } strip)
        {
            var x0 = area.X + strip.X0 * area.Width;
            var x1 = area.X + strip.X1 * area.Width;
            context.DrawRectangle(StripBrush, null, new Rect(x0, area.Y, Math.Max(1, x1 - x0), area.Height));
            context.DrawLine(new Pen(StripLineBrush, 1), new Point(x0, area.Y), new Point(x0, area.Bottom));
            context.DrawLine(new Pen(StripLineBrush, 1), new Point(x1, area.Y), new Point(x1, area.Bottom));
        }

        if (_splitX is { } splitX)
        {
            var x = area.X + splitX * area.Width;
            var pen = new Pen(StripLineBrush, 1, new DashStyle([3, 5], 0));
            context.DrawLine(pen, new Point(x, area.Y), new Point(x, area.Bottom));
        }

        var radius = _points.Count > 700 ? 1.7 : _points.Count > 250 ? 2.1 : 2.6;

        foreach (var point in _points)
        {
            context.DrawEllipse(PointBrush, null, ToScreen(point, area), radius, radius);
        }

        if (_bestPair is { } pair)
        {
            var a = ToScreen(pair.A, area);
            var b = ToScreen(pair.B, area);

            context.DrawLine(new Pen(PairBrush, 2), a, b);
            context.DrawEllipse(PairBrush, null, a, 5.5, 5.5);
            context.DrawEllipse(PairBrush, null, b, 5.5, 5.5);
            context.DrawEllipse(AreaBrush, null, a, 2, 2);
            context.DrawEllipse(AreaBrush, null, b, 2, 2);
        }

        context.DrawRectangle(null, new Pen(BorderBrush, 1.4), new RoundedRect(area, 14));
    }

    private static void DrawGrid(DrawingContext context, Rect area)
    {
        var pen = new Pen(GridBrush, 1);

        for (var i = 1; i < 10; i++)
        {
            var x = area.X + area.Width * i / 10.0;
            var y = area.Y + area.Height * i / 10.0;
            context.DrawLine(pen, new Point(x, area.Y), new Point(x, area.Bottom));
            context.DrawLine(pen, new Point(area.X, y), new Point(area.Right, y));
        }
    }

    private static Point ToScreen(PointD point, Rect area) =>
        new(area.X + point.X * area.Width, area.Y + (1 - point.Y) * area.Height);
}
