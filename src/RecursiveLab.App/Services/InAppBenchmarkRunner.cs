using System.Diagnostics;
using RecursiveLab.Core.Geometry;
using RecursiveLab.Core.Tasks.ClosestPair;
using RecursiveLab.Core.Tasks.Hanoi;
using RecursiveLab.Core.Tasks.HTree;
using RecursiveLab.Core.Tasks.Sudoku;

namespace RecursiveLab.App.Services;

public sealed record BenchmarkProgress(string Stage, int Done, int Total);

public sealed record BenchmarkResults(
    IReadOnlyList<ChartPoint> Htree,
    IReadOnlyList<ChartPoint> Sudoku,
    IReadOnlyList<ChartPoint> Hanoi,
    IReadOnlyList<ClosestPairBenchmarkRow> ClosestPair);

/// <summary>
/// Замеры времени прямо в приложении: те же данные, что даёт консольный стенд,
/// но без запуска внешнего проекта. Теория C·f(n) подбирается по МНК.
/// </summary>
public static class InAppBenchmarkRunner
{
    private const int Seed = 20260219;

    public static BenchmarkResults Run(IProgress<BenchmarkProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        progress?.Report(new BenchmarkProgress("H-дерево", 0, 4));
        var htree = MeasureHtree(cancellationToken);

        progress?.Report(new BenchmarkProgress("Судоку", 1, 4));
        var sudoku = MeasureSudoku(cancellationToken);

        progress?.Report(new BenchmarkProgress("Ближайшая пара", 2, 4));
        var closest = MeasureClosestPair(cancellationToken);

        progress?.Report(new BenchmarkProgress("Ханойские башни", 3, 4));
        var hanoi = MeasureHanoi(cancellationToken);

        progress?.Report(new BenchmarkProgress("Готово", 4, 4));
        return new BenchmarkResults(htree, sudoku, hanoi, closest);
    }

    private static IReadOnlyList<ChartPoint> MeasureHtree(CancellationToken cancellationToken)
    {
        var rows = new List<(int Depth, double Ms)>();

        for (var depth = 0; depth <= HTreeGenerator.MaxDepth - 1; depth++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var batch = depth switch
            {
                <= 2 => 2000,
                <= 4 => 300,
                _ => 30,
            };

            var ms = Measure(() => HTreeGenerator.Generate(1.0, depth), batch, 5);
            rows.Add((depth, ms));
        }

        var c = Fit(rows.Select(row => (double)row.Depth), rows.Select(row => row.Ms), d => Math.Pow(4, d));

        return rows
            .Select(row => new ChartPoint(row.Depth, row.Ms, c * Math.Pow(4, row.Depth)))
            .ToArray();
    }

    private static IReadOnlyList<ChartPoint> MeasureHanoi(CancellationToken cancellationToken)
    {
        var solver = new RecursiveHanoiSolver();
        var rows = new List<(int Disks, double Ms)>();

        for (var disks = 3; disks <= 12; disks++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var ms = Measure(() => solver.Solve(disks), batch: 1, runs: 5);
            rows.Add((disks, ms));
        }

        var c = Fit(rows.Select(row => (double)row.Disks), rows.Select(row => row.Ms), n => Math.Pow(2, n));

        return rows
            .Select(row => new ChartPoint(row.Disks, row.Ms, c * Math.Pow(2, row.Disks)))
            .ToArray();
    }

    private static IReadOnlyList<ChartPoint> MeasureSudoku(CancellationToken cancellationToken)
    {
        const string solvedText =
            "534678912" + "672195348" + "198342567" +
            "859761423" + "426853791" + "713924856" +
            "961537284" + "287419635" + "345286179";

        var solved = SudokuBoard.Parse(solvedText);
        var solver = new BacktrackingSudokuSolver();
        var random = new Random(Seed);
        var empties = new[] { 20, 30, 40, 50, 60 };
        var rows = new List<(int Empties, double Ms)>();

        foreach (var emptyCount in empties)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var holes = Enumerable.Range(0, SudokuBoard.CellCount)
                .OrderBy(_ => random.Next())
                .Take(emptyCount)
                .ToHashSet();

            var board = new SudokuBoard();
            for (var index = 0; index < SudokuBoard.CellCount; index++)
            {
                if (!holes.Contains(index))
                {
                    board.SetGiven(index, solved[index]);
                }
            }

            var result = solver.Solve(board);
            if (!result.Solved)
            {
                continue;
            }

            var ms = Measure(() => solver.Solve(board), batch: 1, runs: 5);
            rows.Add((emptyCount, ms));
        }

        var logs = rows.Select(row => Math.Log(Math.Max(1e-9, row.Ms))).ToArray();
        var meanM = rows.Average(row => (double)row.Empties);
        var meanLog = logs.Average();
        var denominator = rows.Sum(row => Math.Pow(row.Empties - meanM, 2));
        var slope = denominator <= 0
            ? 0
            : rows.Select((row, index) => (row.Empties - meanM) * (logs[index] - meanLog)).Sum() / denominator;
        var intercept = meanLog - slope * meanM;
        var modelC = Math.Exp(intercept);
        var modelA = Math.Exp(slope);

        return rows
            .Select(row => new ChartPoint(
                row.Empties,
                row.Ms,
                modelC * Math.Pow(modelA, row.Empties)))
            .ToArray();
    }

    private static IReadOnlyList<ClosestPairBenchmarkRow> MeasureClosestPair(CancellationToken cancellationToken)
    {
        var finder = new DivideAndConquerClosestPair();
        var rows = new List<ClosestPairBenchmarkRow>();

        for (var n = 100; n <= 2000; n += 100)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var points = GeneratePoints(n, Seed + n);
            var dncBatch = n switch { <= 500 => 300, <= 1000 => 150, _ => 60 };
            var bruteBatch = n switch { <= 300 => 10, <= 800 => 3, _ => 1 };

            _ = finder.Find(points);
            _ = ClosestPairReference.FindBruteForce(points);

            var dncResult = finder.Find(points);
            var bruteResult = ClosestPairReference.FindBruteForce(points);

            var dncMs = Measure(() => finder.Find(points), dncBatch, 3);
            var bruteMs = Measure(() => ClosestPairReference.FindBruteForce(points), bruteBatch, 3);

            rows.Add(new ClosestPairBenchmarkRow(
                n,
                dncMs,
                bruteMs,
                dncResult.DistanceComparisons,
                bruteResult.DistanceComparisons));
        }

        return rows;
    }

    private static PointD[] GeneratePoints(int count, int seed)
    {
        var random = new Random(seed);
        var points = new PointD[count];

        for (var i = 0; i < count; i++)
        {
            points[i] = new PointD(random.NextDouble(), random.NextDouble());
        }

        return points;
    }

    private static double Measure(Action action, int batch, int runs)
    {
        var times = new double[runs];

        for (var run = 0; run < runs; run++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            var stopwatch = Stopwatch.StartNew();

            for (var repeat = 0; repeat < batch; repeat++)
            {
                action();
            }

            stopwatch.Stop();
            times[run] = stopwatch.Elapsed.TotalMilliseconds / batch;
        }

        Array.Sort(times);
        return times[runs / 2];
    }

    private static double Fit(
        IEnumerable<double> xs,
        IEnumerable<double> ys,
        Func<double, double> model)
    {
        double numerator = 0;
        double denominator = 0;

        using var xEnumerator = xs.GetEnumerator();
        using var yEnumerator = ys.GetEnumerator();

        while (xEnumerator.MoveNext() && yEnumerator.MoveNext())
        {
            var f = model(xEnumerator.Current);
            numerator += yEnumerator.Current * f;
            denominator += f * f;
        }

        return denominator <= 0 ? 0 : numerator / denominator;
    }
}
