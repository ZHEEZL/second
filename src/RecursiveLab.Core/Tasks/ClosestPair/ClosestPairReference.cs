using RecursiveLab.Core.Geometry;

namespace RecursiveLab.Core.Tasks.ClosestPair;

/// <summary>
/// Простой перебор O(n²). Это НЕ сдаваемый алгоритм, а эталон для проверки:
/// результат divide and conquer обязан совпадать по расстоянию с перебором.
/// В UI кнопка «Эталон» показывает ответ, с которым можно сверить свой алгоритм.
/// </summary>
public static class ClosestPairReference
{
    public static ClosestPairResult FindBruteForce(
        IReadOnlyList<PointD> points,
        Action<ClosestPairStep>? onStep = null)
    {
        ArgumentNullException.ThrowIfNull(points);

        if (points.Count < 2)
        {
            throw new ArgumentException("Нужно минимум две точки.", nameof(points));
        }

        var started = TimeProvider.System.GetTimestamp();
        long comparisons = 0;
        var bestDistanceSquared = double.PositiveInfinity;
        var bestA = points[0];
        var bestB = points[1];

        for (var i = 0; i < points.Count - 1; i++)
        {
            for (var j = i + 1; j < points.Count; j++)
            {
                comparisons++;
                var distanceSquared = points[i].DistanceSquaredTo(points[j]);
                if (distanceSquared < bestDistanceSquared)
                {
                    bestDistanceSquared = distanceSquared;
                    bestA = points[i];
                    bestB = points[j];
                    onStep?.Invoke(new ClosestPairStep(
                        ClosestPairStepKind.BestChanged,
                        $"Новая лучшая пара: {bestA} — {bestB}",
                        A: bestA,
                        B: bestB,
                        Distance: Math.Sqrt(bestDistanceSquared)));
                }
            }
        }

        var distance = Math.Sqrt(bestDistanceSquared);
        onStep?.Invoke(new ClosestPairStep(
            ClosestPairStepKind.Done,
            $"Перебор завершён: {distance:0.#####}",
            A: bestA,
            B: bestB,
            Distance: distance));

        return new ClosestPairResult(
            bestA,
            bestB,
            distance,
            comparisons,
            comparisons,
            TimeProvider.System.GetElapsedTime(started));
    }
}
