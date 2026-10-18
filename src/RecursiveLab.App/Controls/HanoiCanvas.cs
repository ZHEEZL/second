using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace RecursiveLab.App.Controls;

/// <summary>
/// Рисует три стержня и кольца Ханойской башни. Состояние передаётся
/// снаружи, поэтому контрол можно переиспользовать для анимации ходов.
/// </summary>
public sealed class HanoiCanvas : Control
{
    private static readonly IBrush BaseBrush = new SolidColorBrush(Color.Parse("#2A3C60"));
    private static readonly IBrush RodBrush = new SolidColorBrush(Color.Parse("#3A5585"));
    private static readonly IBrush LabelBrush = new SolidColorBrush(Color.Parse("#8FA3C8"));
    private static readonly Color SmallDiskColor = Color.Parse("#67E8F9");
    private static readonly Color LargeDiskColor = Color.Parse("#A78BFA");
    private static readonly Typeface LabelTypeface =
        new(FontFamily.Parse("Inter, Segoe UI"), FontStyle.Normal, FontWeight.SemiBold);

    private IReadOnlyList<IReadOnlyList<int>> _rods =
    [
        Array.Empty<int>(),
        Array.Empty<int>(),
        Array.Empty<int>(),
    ];

    private int _diskCount;

    public HanoiCanvas()
    {
        ClipToBounds = true;
    }

    public IReadOnlyList<IReadOnlyList<int>> Rods
    {
        get => _rods;
        set
        {
            _rods = value ?? _rods;
            InvalidateVisual();
        }
    }

    public int DiskCount
    {
        get => _diskCount;
        set
        {
            _diskCount = Math.Max(0, value);
            InvalidateVisual();
        }
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        var width = Bounds.Width;
        var height = Bounds.Height;

        if (width <= 1 || height <= 1 || _diskCount == 0)
        {
            return;
        }

        var baseY = height - 58;
        var rodTop = baseY - height * 0.52;
        var spacing = width * 0.28;
        var startX = width / 2 - spacing;
        var maxDiskWidth = Math.Min(spacing * 0.86, width * 0.24);
        var diskHeight = Math.Min(26, (baseY - rodTop) / (_diskCount + 1.4));

        // Основание.
        var baseRect = new Rect(width * 0.08, baseY, width * 0.84, 9);
        context.DrawRectangle(BaseBrush, null, new RoundedRect(baseRect, 4.5));

        // Стержни и подписи.
        for (var rodIndex = 0; rodIndex < 3; rodIndex++)
        {
            var x = startX + rodIndex * spacing;

            context.DrawLine(
                new Pen(RodBrush, 7, lineCap: PenLineCap.Round),
                new Point(x, baseY),
                new Point(x, rodTop));

            var label = new FormattedText(
                ((char)('A' + rodIndex)).ToString(),
                CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                LabelTypeface,
                14,
                LabelBrush);

            context.DrawText(label, new Point(x - label.Width / 2, baseY + 18));
        }

        // Кольца: нижнее кольцо рисуется первым.
        for (var rodIndex = 0; rodIndex < 3; rodIndex++)
        {
            IReadOnlyList<int> rod = rodIndex < _rods.Count ? _rods[rodIndex] : Array.Empty<int>();
            var x = startX + rodIndex * spacing;

            for (var level = 0; level < rod.Count; level++)
            {
                var disk = rod[level];
                var ratio = _diskCount <= 1 ? 1 : (double)(disk - 1) / (_diskCount - 1);
                var diskWidth = maxDiskWidth * (0.36 + 0.64 * ratio);
                var y = baseY - (level + 1) * (diskHeight + 2);

                var color = Lerp(SmallDiskColor, LargeDiskColor, ratio);
                var brush = new SolidColorBrush(color);
                var rect = new Rect(x - diskWidth / 2, y, diskWidth, diskHeight);

                context.DrawRectangle(brush, null, new RoundedRect(rect, diskHeight / 2));
                context.DrawRectangle(
                    null,
                    new Pen(new SolidColorBrush(Color.FromArgb(70, 255, 255, 255)), 1),
                    new RoundedRect(rect, diskHeight / 2));
            }
        }
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
