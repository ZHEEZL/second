namespace RecursiveLab.Core.Geometry;

/// <summary>Точка в мировых координатах (не зависит от UI-фреймворка).</summary>
public readonly record struct PointD(double X, double Y)
{
    public double DistanceSquaredTo(PointD other)
    {
        var dx = X - other.X;
        var dy = Y - other.Y;
        return dx * dx + dy * dy;
    }

    public double DistanceTo(PointD other) => Math.Sqrt(DistanceSquaredTo(other));

    public override string ToString() => $"({X:0.###}; {Y:0.###})";
}
