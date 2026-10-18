using RecursiveLab.Core.Geometry;

namespace RecursiveLab.Core.Tasks.ClosestPair;

/// <summary>Тип шага алгоритма ближайшей пары (для анимации и лога).</summary>
public enum ClosestPairStepKind
{
    /// <summary>Массив разделён по медиане X.</summary>
    Split,

    /// <summary>Обработана маленькая часть перебором.</summary>
    BaseCase,

    /// <summary>Просматривается полоса вокруг линии раздела.</summary>
    Strip,

    /// <summary>Найдена пара лучше текущей.</summary>
    BestChanged,

    /// <summary>Алгоритм завершён.</summary>
    Done,
}

/// <summary>
/// Шаг алгоритма. Геометрия необязательна: UI умеет рисовать и просто сообщение.
/// </summary>
public readonly record struct ClosestPairStep(
    ClosestPairStepKind Kind,
    string Message,
    double X0 = double.NaN,
    double X1 = double.NaN,
    PointD? A = null,
    PointD? B = null,
    double Distance = double.NaN);

public sealed record ClosestPairResult(
    PointD A,
    PointD B,
    double Distance,
    long DistanceComparisons,
    long RecursiveCalls,
    TimeSpan Elapsed);

/// <summary>
/// Контракт алгоритма «ближайшая пара точек» (divide and conquer).
/// Реализацию пишет участник команды (файл DivideAndConquerClosestPair.cs).
/// </summary>
public interface IClosestPairFinder
{
    string Name { get; }

    /// <summary>
    /// Возвращает ближайшую пару. Точки не изменяются.
    /// Если точек меньше двух — ArgumentException.
    /// </summary>
    ClosestPairResult Find(
        IReadOnlyList<PointD> points,
        Action<ClosestPairStep>? onStep = null,
        CancellationToken cancellationToken = default);
}
