using RecursiveLab.Core.Geometry;
using RecursiveLab.Core.Recursion;

namespace RecursiveLab.Core.Tasks.HTree;

/// <summary>
/// H-дерево (H-tree fractal) — классический рекурсивный фрактал.
///
/// Базовый случай: одна буква H (две вертикальные стойки + перекладина).
/// Рекурсивный случай: в каждом из четырёх концов буквы H строится такая же
/// буква H, уменьшенная в sqrt(2) раз. Глубина 0 — только одна H.
///
/// Отрезки возвращаются в порядке рекурсивного обхода (DFT):
/// сначала ствол, затем четыре поддерева. Именно этот порядок использует
/// анимация «рисование по рекурсии».
/// </summary>
public static class HTreeGenerator
{
    /// <summary>Во сколько раз меньше каждая следующая H: 1 / sqrt(2).</summary>
    public const double ChildScale = 0.7071067811865476;

    /// <summary>Максимальная поддерживаемая глубина (65 535 отрезков — уже тяжело для UI).</summary>
    public const int MaxDepth = 7;

    /// <summary>
    /// Строит H-дерево с центром в (0; 0) и размером <paramref name="size"/>.
    /// Если передан <paramref name="tracer"/>, каждый вызов Build записывается в дерево рекурсии.
    /// </summary>
    public static IReadOnlyList<Segment> Generate(double size = 1.0, int depth = 5, IRecursionTracer? tracer = null)
    {
        if (size <= 0 || double.IsNaN(size) || double.IsInfinity(size))
        {
            throw new ArgumentOutOfRangeException(nameof(size), "Размер должен быть положительным.");
        }

        if (depth < 0 || depth > MaxDepth)
        {
            throw new ArgumentOutOfRangeException(nameof(depth), $"Глубина должна быть в диапазоне 0..{MaxDepth}.");
        }

        // 4^(depth+1) - 1 отрезков. Для depth = 7 это 65 535.
        var capacity = SegmentCount(depth);
        var segments = new List<Segment>(capacity);
        Build(0, 0, size, 0, depth, segments, tracer);
        return segments;
    }

    private static void Build(
        double cx,
        double cy,
        double size,
        int level,
        int maxDepth,
        List<Segment> segments,
        IRecursionTracer? tracer)
    {
        var half = size / 2.0;

        // Дерево рекурсии: один узел = один вызов Build (одна буква H).
        var node = tracer?.Enter(
            $"H{level}",
            $"Build(центр ({cx:0.###}; {cy:0.###}), размер {size:0.###}, уровень {level})") ?? 0;

        // Стойка H слева.
        segments.Add(new Segment(
            new PointD(cx - half, cy - half),
            new PointD(cx - half, cy + half),
            level));

        // Стойка H справа.
        segments.Add(new Segment(
            new PointD(cx + half, cy - half),
            new PointD(cx + half, cy + half),
            level));

        // Перекладина H.
        segments.Add(new Segment(
            new PointD(cx - half, cy),
            new PointD(cx + half, cy),
            level));

        if (level >= maxDepth)
        {
            tracer?.Exit(node, RecursionOutcome.BaseCase, "3 отрезка; глубина достигнута — дальше не идём");
            return;
        }

        // Четыре поддерева — в каждом конце буквы H.
        var childSize = size * ChildScale;
        var nextLevel = level + 1;

        Build(cx - half, cy - half, childSize, nextLevel, maxDepth, segments, tracer);
        Build(cx + half, cy - half, childSize, nextLevel, maxDepth, segments, tracer);
        Build(cx - half, cy + half, childSize, nextLevel, maxDepth, segments, tracer);
        Build(cx + half, cy + half, childSize, nextLevel, maxDepth, segments, tracer);

        tracer?.Exit(node, RecursionOutcome.Completed, "3 отрезка + 4 поддерева");
    }

    /// <summary>Количество отрезков: 4^(depth+1) - 1.</summary>
    public static int SegmentCount(int depth)
    {
        var count = 1;
        for (var i = 0; i <= depth; i++)
        {
            count *= 4;
        }

        return count - 1;
    }

    /// <summary>Количество букв H: (4^(depth+1) - 1) / 3.</summary>
    public static long HShapeCount(int depth)
    {
        long count = 0;
        long levelCount = 1;
        for (var i = 0; i <= depth; i++)
        {
            count += levelCount;
            levelCount *= 4;
        }

        return count;
    }

    /// <summary>Габариты фрактала в мировых координатах.</summary>
    public static (PointD Min, PointD Max) GetBounds(IReadOnlyList<Segment> segments)
    {
        if (segments.Count == 0)
        {
            return (new PointD(0, 0), new PointD(0, 0));
        }

        double minX = double.MaxValue, minY = double.MaxValue;
        double maxX = double.MinValue, maxY = double.MinValue;

        foreach (var segment in segments)
        {
            minX = Math.Min(minX, Math.Min(segment.Start.X, segment.End.X));
            minY = Math.Min(minY, Math.Min(segment.Start.Y, segment.End.Y));
            maxX = Math.Max(maxX, Math.Max(segment.Start.X, segment.End.X));
            maxY = Math.Max(maxY, Math.Max(segment.Start.Y, segment.End.Y));
        }

        return (new PointD(minX, minY), new PointD(maxX, maxY));
    }
}
