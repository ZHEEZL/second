using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using RecursiveLab.Core.Geometry;
using RecursiveLab.Core.Tasks.HTree;

namespace RecursiveLab.App.Controls;

/// <summary>
/// Рисует H-дерево по отрезкам, которые вернул рекурсивный генератор.
/// Отдельный контрол: не зависит от окна, поэтому его можно тестировать
/// и переиспользовать (например, для экспорта PNG).
/// </summary>
public sealed class HTreeCanvas : Control
{
    private static readonly Color StartColor = Color.Parse("#7DD3FC");
    private static readonly Color MiddleColor = Color.Parse("#818CF8");
    private static readonly Color EndColor = Color.Parse("#C084FC");

    private readonly Dictionary<int, Pen> _mainPens = new();
    private readonly Dictionary<int, Pen> _glowPens = new();

    private IReadOnlyList<Segment> _segments = Array.Empty<Segment>();
    private (PointD Min, PointD Max) _worldBounds;
    private int _visibleCount;
    private bool _showGlow = true;
    private int _maxLevel;

    public HTreeCanvas()
    {
        ClipToBounds = true;
    }

    public IReadOnlyList<Segment> Segments
    {
        get => _segments;
        set
        {
            _segments = value ?? Array.Empty<Segment>();
            _worldBounds = HTreeGenerator.GetBounds(_segments);
            _maxLevel = _segments.Count == 0 ? 0 : _segments[^1].Depth;
            _mainPens.Clear();
            _glowPens.Clear();
            _visibleCount = Math.Clamp(_visibleCount, 0, _segments.Count);
            InvalidateVisual();
        }
    }

    public int VisibleCount
    {
        get => _visibleCount;
        set
        {
            var clamped = Math.Clamp(value, 0, _segments.Count);
            if (clamped == _visibleCount)
            {
                return;
            }

            _visibleCount = clamped;
            InvalidateVisual();
        }
    }

    public bool ShowGlow
    {
        get => _showGlow;
        set
        {
            if (_showGlow == value)
            {
                return;
            }

            _showGlow = value;
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

        DrawAmbientGlow(context, width, height);
        DrawGrid(context, width, height);

        if (_segments.Count == 0 || _visibleCount == 0)
        {
            return;
        }

        var padding = Math.Min(width, height) * 0.09;
        var worldWidth = Math.Max(_worldBounds.Max.X - _worldBounds.Min.X, 1e-9);
        var worldHeight = Math.Max(_worldBounds.Max.Y - _worldBounds.Min.Y, 1e-9);
        var scale = Math.Min(
            Math.Max(width - padding * 2, 1) / worldWidth,
            Math.Max(height - padding * 2, 1) / worldHeight);

        var offsetX = width / 2 - worldWidth * scale / 2 - _worldBounds.Min.X * scale;
        var offsetY = height / 2 - worldHeight * scale / 2 - _worldBounds.Min.Y * scale;

        var drawGlow = _showGlow && _segments.Count <= 3000;

        for (var i = 0; i < _visibleCount; i++)
        {
            var segment = _segments[i];
            var start = new Point(
                segment.Start.X * scale + offsetX,
                segment.Start.Y * scale + offsetY);
            var end = new Point(
                segment.End.X * scale + offsetX,
                segment.End.Y * scale + offsetY);

            if (drawGlow)
            {
                context.DrawLine(GlowPen(segment.Depth), start, end);
            }

            context.DrawLine(MainPen(segment.Depth), start, end);
        }
    }

    private static void DrawAmbientGlow(DrawingContext context, double width, double height)
    {
        var center = new Point(width / 2, height / 2);
        var radius = Math.Min(width, height) * 0.62;

        for (var i = 3; i >= 1; i--)
        {
            var alpha = (byte)(4 + i * 3);
            var brush = new SolidColorBrush(Color.FromArgb(alpha, 0x22, 0xD3, 0xEE));
            context.DrawEllipse(brush, null, center, radius * i / 3, radius * i / 3);
        }
    }

    private static void DrawGrid(DrawingContext context, double width, double height)
    {
        var pen = new Pen(new SolidColorBrush(Color.FromArgb(9, 0xC7, 0xD6, 0xFF)), 1);

        const double step = 30;

        for (var x = step; x < width; x += step)
        {
            context.DrawLine(pen, new Point(x, 0), new Point(x, height));
        }

        for (var y = step; y < height; y += step)
        {
            context.DrawLine(pen, new Point(0, y), new Point(width, y));
        }
    }

    private Pen MainPen(int level)
    {
        if (_mainPens.TryGetValue(level, out var cached))
        {
            return cached;
        }

        var t = _maxLevel == 0 ? 0 : (double)level / _maxLevel;
        var color = Gradient(t);
        var alpha = (byte)Math.Clamp(255 - level * 22, 140, 255);
        var thickness = Math.Max(0.9, 6.4 * Math.Pow(0.62, level));
        var pen = new Pen(
            new SolidColorBrush(Color.FromArgb(alpha, color.R, color.G, color.B)),
            thickness,
            lineCap: PenLineCap.Round);
        _mainPens[level] = pen;
        return pen;
    }

    private Pen GlowPen(int level)
    {
        if (_glowPens.TryGetValue(level, out var cached))
        {
            return cached;
        }

        var t = _maxLevel == 0 ? 0 : (double)level / _maxLevel;
        var color = Gradient(t);
        var thickness = Math.Max(0.9, 6.4 * Math.Pow(0.62, level)) + 5.5;
        var pen = new Pen(
            new SolidColorBrush(Color.FromArgb(24, color.R, color.G, color.B)),
            thickness,
            lineCap: PenLineCap.Round);
        _glowPens[level] = pen;
        return pen;
    }

    private static Color Gradient(double t)
    {
        t = Math.Clamp(t, 0, 1);

        return t < 0.58
            ? Lerp(StartColor, MiddleColor, t / 0.58)
            : Lerp(MiddleColor, EndColor, (t - 0.58) / 0.42);
    }

    private static Color Lerp(Color from, Color to, double t)
    {
        t = Math.Clamp(t, 0, 1);
        return Color.FromArgb(
            (byte)(from.A + (to.A - from.A) * t),
            (byte)(from.R + (to.R - from.R) * t),
            (byte)(from.G + (to.G - from.G) * t),
            (byte)(from.B + (to.B - from.B) * t));
    }
}
