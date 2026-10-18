namespace RecursiveLab.Core.Tasks.ClosestPair;

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

        // TODO(ближайшая пара): реализовать алгоритм.
        // Для проверки результата можно сравнить с ClosestPairReference.FindBruteForce.
        throw new NotImplementedException(
            "Divide and conquer для ближайшей пары ещё не реализован. " +
            "Откройте src/RecursiveLab.Core/Tasks/ClosestPair/DivideAndConquerClosestPair.cs и заполните TODO.");
    }
}
