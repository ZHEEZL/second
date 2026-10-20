using System.Diagnostics;
using RecursiveLab.Core.Geometry;

namespace RecursiveLab.Core.Tasks.ClosestPair;

record struct Pair(PointD A, PointD B, double DistSquared);

/// <summary>
/// ЗАГЛУШКА для участника команды, отвечающего за «ближайшую пару точек».
///
/// Что нужно сделать:
///   1. Реализовать divide and conquer:
///      — отсортировать точки по X;
///      — рекурсивно решить левую и правую половины;
///      — взять минимум из двух половин;
///      — просмотреть полосу шириной 2·d вокруг линии раздела (точки в полосе
///        сортируются по Y) и проверить не более 7 следующих точек.
///   2. Вернуть ближайшую пару и число сравнений расстояний.
///   3. Вызывать onStep на этапах Split / BaseCase / Strip / BestChanged —
///      тогда UI покажет разбиение и полосу.
///
/// Не изменяйте сигнатуру Find — на неё рассчитан UI.
/// </summary>
public sealed class DivideAndConquerClosestPair : IClosestPairFinder
{
    public string Name => "Разделяй и властвуй (O(n log n))";

    public ClosestPairResult Find(
        IReadOnlyList<Geometry.PointD> points,
        Action<ClosestPairStep>? onStep = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(points);

        if (points.Count < 2)
        {
            throw new ArgumentException("Нужно минимум две точки.", nameof(points));
        }

        var arr = points
            .OrderBy(p => p.X)
            .ThenBy(p => p.Y)
            .ToArray();

        var started = Stopwatch.StartNew();
        long comparisons = 0;
        long calls = 0;

        var best = Solve(arr, 0, arr.Length, ref comparisons, ref calls, onStep, cancellationToken);
        var distance = Math.Sqrt(best.DistSquared);

        onStep?.Invoke(new ClosestPairStep(
            ClosestPairStepKind.Done,
            "Готово",
            A: best.A,
            B: best.B,
            Distance: distance));

        return new ClosestPairResult(best.A, best.B, distance, comparisons, calls, started.Elapsed);

    }

    private static Pair Solve(PointD[] arr, int lo, int hi, ref long comparisons, ref long calls, Action<ClosestPairStep>? onStep,
        CancellationToken cancellationToken)
    {
        calls++;
        cancellationToken.ThrowIfCancellationRequested();

        var count = hi - lo;

        if (count <= 3)
        {
            var baseBest = new Pair(default, default, double.PositiveInfinity);

            for (var i = lo; i < hi - 1; i++)
            {
                for (var j = i + 1; j < hi; j++)
                {
                    comparisons++;
                    var d = arr[i].DistanceSquaredTo(arr[j]);
                    if (d < baseBest.DistSquared)
                    {
                        baseBest = new Pair(arr[i], arr[j], d);
                    }
                }
            }

            onStep?.Invoke(new ClosestPairStep(
                ClosestPairStepKind.BaseCase,
                $"База: {count} точек",
                A: baseBest.A,
                B: baseBest.B,
                Distance: Math.Sqrt(baseBest.DistSquared)));

            return baseBest;
        }

        var mid = lo + count / 2;
        var midX = arr[mid].X;

        onStep?.Invoke(new ClosestPairStep(
            ClosestPairStepKind.Split,
            $"Делим [{lo}; {hi}) по x = {midX:0.###}",
            X0: midX));

        var left = Solve(arr, lo, mid, ref comparisons, ref calls, onStep, cancellationToken);
        var right = Solve(arr, mid, hi, ref comparisons, ref calls, onStep, cancellationToken);

        var best = left.DistSquared <= right.DistSquared ? left : right;

        onStep?.Invoke(new ClosestPairStep(
            ClosestPairStepKind.BestChanged,
            "Минимум из половин",
            A: best.A,
            B: best.B,
            Distance: Math.Sqrt(best.DistSquared)));

        var strip = new List<PointD>();
        for (var i = lo; i < hi; i++)
        {
            var dx = arr[i].X - midX;
            if (dx * dx < best.DistSquared)
            {
                strip.Add(arr[i]);
            }
        }

        strip.Sort((a, b) => a.Y.CompareTo(b.Y));

        var currentDistance = Math.Sqrt(best.DistSquared);
        onStep?.Invoke(new ClosestPairStep(
            ClosestPairStepKind.Strip,
            $"Полоса: {strip.Count} точек",
            X0: midX - currentDistance,
            X1: midX + currentDistance));

        for (var i = 0; i < strip.Count; i++)
        {
            for (var j = i + 1; j < strip.Count; j++)
            {
                var dy = strip[j].Y - strip[i].Y;
                if (dy * dy >= best.DistSquared) break;

                comparisons++;
                var d = strip[i].DistanceSquaredTo(strip[j]);
                if (d < best.DistSquared)
                {
                    best = new Pair(strip[i], strip[j], d);
                    onStep?.Invoke(new ClosestPairStep(
                        ClosestPairStepKind.BestChanged,
                        "Найдена пара лучше",
                        A: best.A,
                        B: best.B,
                        Distance: Math.Sqrt(best.DistSquared)));
                }
            }
        }
        return best;

    }
}
