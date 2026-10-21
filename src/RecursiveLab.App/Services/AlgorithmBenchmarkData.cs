using System.Globalization;

namespace RecursiveLab.App.Services;

public readonly record struct ChartPoint(double X, double Actual, double Theory);

/// <summary>
/// Читает CSV стенда для H-дерева, судоку и Ханоя. Для ближайшей пары
/// используется отдельный <see cref="ClosestPairBenchmarkData"/>.
/// </summary>
public static class AlgorithmBenchmarkData
{
    public static IReadOnlyList<ChartPoint> Htree { get; } = Load("htree.csv", x: 0, actual: 2, theory: 3);

    public static IReadOnlyList<ChartPoint> Hanoi { get; } = Load("hanoi.csv", x: 0, actual: 2, theory: 3);

    public static IReadOnlyList<ChartPoint> Sudoku { get; } = Load(
        "sudoku.csv",
        x: 0,
        actual: 1,
        theory: 4);

    private static IReadOnlyList<ChartPoint> Load(
        string fileName,
        int x,
        int actual,
        int theory)
    {
        var path = FindFile(fileName);
        if (path is null)
        {
            return Array.Empty<ChartPoint>();
        }

        var result = new List<ChartPoint>();

        try
        {
            foreach (var line in File.ReadLines(path).Skip(1))
            {
                var parts = line.Split(';');
                if (parts.Length <= Math.Max(x, Math.Max(actual, theory)))
                {
                    continue;
                }

                if (!double.TryParse(parts[x], NumberStyles.Float, CultureInfo.InvariantCulture, out var xValue) ||
                    !double.TryParse(parts[actual], NumberStyles.Float, CultureInfo.InvariantCulture, out var actualValue) ||
                    !double.TryParse(parts[theory], NumberStyles.Float, CultureInfo.InvariantCulture, out var theoryValue))
                {
                    continue;
                }

                result.Add(new ChartPoint(xValue, actualValue, theoryValue));
            }
        }
        catch
        {
            return Array.Empty<ChartPoint>();
        }

        return result.OrderBy(point => point.X).ToArray();
    }

    private static string? FindFile(string fileName)
    {
        var candidates = new List<string>();

        if (RepoPaths.Root is { } root)
        {
            candidates.Add(Path.Combine(root, "benchmarks", "results", fileName));
        }

        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            candidates.Add(Path.Combine(directory.FullName, "benchmarks", "results", fileName));
            candidates.Add(Path.Combine(directory.FullName, "results", fileName));
            directory = directory.Parent;
        }

        return candidates.FirstOrDefault(File.Exists);
    }
}
