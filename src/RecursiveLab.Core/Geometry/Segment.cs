namespace RecursiveLab.Core.Geometry;

/// <summary>
/// Отрезок фрактала. <paramref name="Depth"/> — уровень рекурсии:
/// 0 — ствол (первая буква H), дальше значение растёт.
/// </summary>
public readonly record struct Segment(PointD Start, PointD End, int Depth);
