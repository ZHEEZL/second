using System.Globalization;

namespace RecursiveLab.App.Services;

public readonly record struct ClosestPairBenchmarkRow(
    int N,
    double DncMs,
    double BruteMs,
    long DncComparisons,
    long BruteComparisons);

/// <summary>
/// Читает результаты стенда benchmarks/results/closest-pair.csv, чтобы графики
/// в приложении совпадали с отчётом. Если файла нет — графики покажут подсказку.
/// </summary>
public static class ClosestPairBenchmarkData
{
    private const string RelativePath = "benchmarks/results/closest-pair.csv";
    private static readonly Lazy<IReadOnlyList<ClosestPairBenchmarkRow>> Cache = new(LoadCore);

    public static string? SourcePath { get; } = FindSource();

    public static IReadOnlyList<ClosestPairBenchmarkRow> Rows => Cache.Value;

    private static IReadOnlyList<ClosestPairBenchmarkRow> LoadCore()
    {
        if (SourcePath is null)
        {
            return Array.Empty<ClosestPairBenchmarkRow>();
        }

        var rows = new List<ClosestPairBenchmarkRow>();

        try
        {
            foreach (var line in File.ReadLines(SourcePath).Skip(1))
            {
                var parts = line.Split(';');
                if (parts.Length < 8)
                {
                    continue;
                }

                if (!int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) ||
                    !double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var dncMs) ||
                    !double.TryParse(parts[4], NumberStyles.Float, CultureInfo.InvariantCulture, out var bruteMs) ||
                    !long.TryParse(parts[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out var dncComparisons) ||
                    !long.TryParse(parts[6], NumberStyles.Integer, CultureInfo.InvariantCulture, out var bruteComparisons))
                {
                    continue;
                }

                rows.Add(new ClosestPairBenchmarkRow(n, dncMs, bruteMs, dncComparisons, bruteComparisons));
            }
        }
        catch
        {
            return Array.Empty<ClosestPairBenchmarkRow>();
        }

        return rows.OrderBy(row => row.N).ToArray();
    }

    private static string? FindSource()
    {
        var candidates = new List<string>();

        if (RepoPaths.Root is { } root)
        {
            candidates.Add(Path.Combine(root, RelativePath));
        }

        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            candidates.Add(Path.Combine(directory.FullName, RelativePath));
            candidates.Add(Path.Combine(directory.FullName, "results", "closest-pair.csv"));
            directory = directory.Parent;
        }

        return candidates.FirstOrDefault(File.Exists);
    }
}
