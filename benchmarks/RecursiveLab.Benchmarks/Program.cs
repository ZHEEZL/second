using System.Diagnostics;
using System.Globalization;
using System.Text;
using RecursiveLab.Core.Geometry;
using RecursiveLab.Core.Tasks.ClosestPair;
using ScottPlot;

namespace RecursiveLab.Benchmarks;

/// <summary>
/// Стенд для замера времени алгоритма «ближайшая пара точек».
/// Методика как в лабораторной №1: фиксированный seed, прогрев, Runs повторов,
/// медиана, CSV с результатами и PNG-графики ScottPlot.
/// </summary>
internal static class Program
{
    private const int MinN = 100;
    private const int MaxN = 2000;
    private const int Step = 100;
    private const int Runs = 5;
    private const int Seed = 20260219;

    private static int Main()
    {
        Console.OutputEncoding = Encoding.UTF8;

        var root = FindRepositoryRoot();
        var resultsDir = Path.Combine(root, "benchmarks", "results");
        var chartDir = Path.Combine(root, "docs", "charts");
        Directory.CreateDirectory(resultsDir);
        Directory.CreateDirectory(chartDir);

        var finder = new DivideAndConquerClosestPair();
        var rows = new List<Row>();

        Console.WriteLine("=== Ближайшая пара точек: замеры ===");
        Console.WriteLine($"n = {MinN}..{MaxN}, шаг {Step}, повторов {Runs}, seed {Seed}");
        Console.WriteLine();

        var total = Stopwatch.StartNew();

        for (var n = MinN; n <= MaxN; n += Step)
        {
            var points = GeneratePoints(n, Seed + n);
            var dncBatch = BatchForDnc(n);
            var bruteBatch = BatchForBrute(n);

            // Прогрев JIT/кэша на этом размере — вне замера.
            _ = finder.Find(points);
            _ = ClosestPairReference.FindBruteForce(points);

            var dnc = Measure(() => finder.Find(points), dncBatch, Runs);
            var brute = Measure(() => ClosestPairReference.FindBruteForce(points), bruteBatch, Runs);

            var row = new Row(
                n,
                dnc.MedianMs,
                dnc.StdDevMs,
                dnc.Last.DistanceComparisons,
                brute.MedianMs,
                brute.StdDevMs,
                brute.Last.DistanceComparisons);

            rows.Add(row);

            Console.WriteLine(
                $"n={n,5}  D&C={row.DncMs,9:0.000} мс  brute={row.BruteMs,9:0.000} мс  " +
                $"ускорение ×{row.Speedup,7:0.0}  сравнений D&C={row.DncComparisons,8:N0}");
        }

        total.Stop();

        OtherBenchmarks.Run(resultsDir, chartDir);

        var csvPath = Path.Combine(resultsDir, "closest-pair.csv");
        WriteCsv(csvPath, rows);

        PlotTime(rows, Path.Combine(chartDir, "closest-pair-time.png"));
        PlotComparisons(rows, Path.Combine(chartDir, "closest-pair-comparisons.png"));
        PlotSpeedup(rows, Path.Combine(chartDir, "closest-pair-speedup.png"));
        PlotTheory(rows, dnc: true, Path.Combine(chartDir, "closest-pair-dnc-theory.png"));
        PlotTheory(rows, dnc: false, Path.Combine(chartDir, "closest-pair-brute-theory.png"));
        WriteSummary(resultsDir, rows);

        Console.WriteLine();
        Console.WriteLine($"Готово за {total.Elapsed.TotalSeconds:0.0} с");
        Console.WriteLine($"CSV: {csvPath}");
        Console.WriteLine($"График времени: {Path.Combine(chartDir, "closest-pair-time.png")}");
        Console.WriteLine($"График сравнений: {Path.Combine(chartDir, "closest-pair-comparisons.png")}");
        Console.WriteLine($"График ускорения: {Path.Combine(chartDir, "closest-pair-speedup.png")}");
        Console.WriteLine($"Теория D&C: {Path.Combine(chartDir, "closest-pair-dnc-theory.png")}");
        Console.WriteLine($"Теория перебора: {Path.Combine(chartDir, "closest-pair-brute-theory.png")}");
        return 0;
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

    private static int BatchForDnc(int n) => n switch
    {
        <= 500 => 500,
        <= 1000 => 200,
        _ => 80,
    };

    private static int BatchForBrute(int n) => n switch
    {
        <= 300 => 20,
        <= 800 => 5,
        _ => 1,
    };

    private static Measurement<T> Measure<T>(Func<T> action, int batch, int runs)
    {
        var times = new double[runs];
        var last = default(T)!;

        for (var run = 0; run < runs; run++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            var stopwatch = Stopwatch.StartNew();

            for (var repeat = 0; repeat < batch; repeat++)
            {
                last = action();
            }

            stopwatch.Stop();
            times[run] = stopwatch.Elapsed.TotalMilliseconds / batch;
        }

        Array.Sort(times);
        var median = times[runs / 2];

        var sumSquares = 0.0;
        foreach (var time in times)
        {
            var diff = time - median;
            sumSquares += diff * diff;
        }

        var stdDev = Math.Sqrt(sumSquares / Math.Max(1, runs - 1));
        return new Measurement<T>(median, stdDev, last);
    }

    private static void WriteCsv(string path, IReadOnlyList<Row> rows)
    {
        var builder = new StringBuilder();
        builder.AppendLine("n;dnc_ms;dnc_std_ms;dnc_comparisons;brute_ms;brute_std_ms;brute_comparisons;speedup");

        foreach (var row in rows)
        {
            builder.AppendLine(string.Join(';',
                row.N.ToString(CultureInfo.InvariantCulture),
                row.DncMs.ToString("0.######", CultureInfo.InvariantCulture),
                row.DncStdMs.ToString("0.######", CultureInfo.InvariantCulture),
                row.DncComparisons.ToString(CultureInfo.InvariantCulture),
                row.BruteMs.ToString("0.######", CultureInfo.InvariantCulture),
                row.BruteStdMs.ToString("0.######", CultureInfo.InvariantCulture),
                row.BruteComparisons.ToString(CultureInfo.InvariantCulture),
                row.Speedup.ToString("0.###", CultureInfo.InvariantCulture)));
        }

        File.WriteAllText(path, builder.ToString(), new UTF8Encoding(false));
    }

    private static void PlotTime(IReadOnlyList<Row> rows, string path)
    {
        var plot = CreatePlot();
        var xs = rows.Select(row => (double)row.N).ToArray();

        var dnc = plot.Add.Scatter(xs, rows.Select(row => row.DncMs).ToArray());
        dnc.LegendText = "Divide and conquer";
        dnc.Color = Color.FromHex("#60A5FA");
        dnc.LineWidth = 2.2f;
        dnc.MarkerSize = 5;

        var brute = plot.Add.Scatter(xs, rows.Select(row => row.BruteMs).ToArray());
        brute.LegendText = "Полный перебор";
        brute.Color = Color.FromHex("#FB923C");
        brute.LineWidth = 2.2f;
        brute.MarkerSize = 5;

        plot.Title("Ближайшая пара: время работы");
        plot.XLabel("Количество точек n");
        plot.YLabel("Время, мс");
        plot.ShowLegend(Alignment.UpperLeft);
        plot.Axes.AutoScale();
        plot.SavePng(path, 1000, 620);
    }

    private static void PlotComparisons(IReadOnlyList<Row> rows, string path)
    {
        var plot = CreatePlot();
        var xs = rows.Select(row => (double)row.N).ToArray();

        var dnc = plot.Add.Scatter(xs, rows.Select(row => (double)row.DncComparisons).ToArray());
        dnc.LegendText = "Divide and conquer";
        dnc.Color = Color.FromHex("#34D399");
        dnc.LineWidth = 2.2f;
        dnc.MarkerSize = 5;

        var brute = plot.Add.Scatter(xs, rows.Select(row => (double)row.BruteComparisons).ToArray());
        brute.LegendText = "Полный перебор";
        brute.Color = Color.FromHex("#F87171");
        brute.LineWidth = 2.2f;
        brute.MarkerSize = 5;

        plot.Title("Ближайшая пара: число сравнений расстояний");
        plot.XLabel("Количество точек n");
        plot.YLabel("Сравнений");
        plot.ShowLegend(Alignment.UpperLeft);
        plot.Axes.AutoScale();
        plot.SavePng(path, 1000, 620);
    }

    private static void PlotSpeedup(IReadOnlyList<Row> rows, string path)
    {
        var plot = CreatePlot();
        var xs = rows.Select(row => (double)row.N).ToArray();

        var speedup = plot.Add.Scatter(xs, rows.Select(row => row.Speedup).ToArray());
        speedup.LegendText = "Перебор / D&C";
        speedup.Color = Color.FromHex("#C084FC");
        speedup.LineWidth = 2.2f;
        speedup.MarkerSize = 5;

        plot.Title("Ближайшая пара: ускорение относительно перебора");
        plot.XLabel("Количество точек n");
        plot.YLabel("Во сколько раз быстрее, ×");
        plot.ShowLegend(Alignment.UpperLeft);
        plot.Axes.AutoScale();
        plot.SavePng(path, 1000, 620);
    }

    private static void PlotTheory(IReadOnlyList<Row> rows, bool dnc, string path)
    {
        var model = dnc
            ? (Func<double, double>)(n => n * Math.Log2(Math.Max(2, n)))
            : n => n * n;

        var actualTimes = dnc
            ? rows.Select(row => row.DncMs).ToArray()
            : rows.Select(row => row.BruteMs).ToArray();

        var ns = rows.Select(row => (double)row.N).ToArray();
        var c = FitConstant(ns, actualTimes, model);
        var mse = Mse(ns, actualTimes, c, model);

        var plot = CreatePlot();

        var actual = plot.Add.Scatter(ns, actualTimes);
        actual.LegendText = "Эксперимент";
        actual.Color = dnc ? Color.FromHex("#60A5FA") : Color.FromHex("#FB923C");
        actual.LineWidth = 2.2f;
        actual.MarkerSize = 5;

        var theoryNs = Enumerable.Range(0, 191)
            .Select(i => MinN + (MaxN - MinN) * i / 190.0)
            .ToArray();
        var theoryTimes = theoryNs.Select(n => c * model(n)).ToArray();

        var theory = plot.Add.Scatter(theoryNs, theoryTimes);
        theory.LegendText = dnc ? "Теория: C · n · log₂n" : "Теория: C · n²";
        theory.Color = Color.FromHex("#C084FC");
        theory.LineWidth = 2.0f;
        theory.MarkerSize = 0;
        theory.LinePattern = LinePattern.Dashed;

        var annotation = plot.Add.Annotation(
            $"C = {c:0.####E+00} мс\nMSE = {mse:0.####E+00}");
        annotation.LabelBackgroundColor = Color.FromHex("#262A35");
        annotation.LabelBorderColor = Color.FromHex("#4B5563");
        annotation.LabelFontColor = Color.FromHex("#E2E8F0");
        annotation.LabelFontSize = 11;

        plot.Title(dnc
            ? "Divide and conquer: эксперимент и теория"
            : "Полный перебор: эксперимент и теория");
        plot.XLabel("Количество точек n");
        plot.YLabel("Время, мс");
        plot.ShowLegend(Alignment.UpperLeft);
        plot.Axes.AutoScale();
        plot.SavePng(path, 1000, 620);
    }

    private static Plot CreatePlot()
    {
        var plot = new Plot();
        plot.FigureBackground.Color = Color.FromHex("#1F2229");
        plot.DataBackground.Color = Color.FromHex("#181A20");
        plot.Axes.Color(Color.FromHex("#E2E8F0"));
        plot.Grid.MajorLineColor = Color.FromHex("#2D323F");
        return plot;
    }

    private static void WriteSummary(string resultsDir, IReadOnlyList<Row> rows)
    {
        var dncC = FitConstant(rows.Select(r => (double)r.N), rows.Select(r => r.DncMs), n => n * Math.Log2(Math.Max(2, n)));
        var bruteC = FitConstant(rows.Select(r => (double)r.N), rows.Select(r => r.BruteMs), n => n * n);
        var dncMse = Mse(rows.Select(r => (double)r.N), rows.Select(r => r.DncMs), dncC, n => n * Math.Log2(Math.Max(2, n)));
        var bruteMse = Mse(rows.Select(r => (double)r.N), rows.Select(r => r.BruteMs), bruteC, n => n * n);

        var last = rows[^1];
        var builder = new StringBuilder();
        builder.AppendLine("# Результаты замеров: ближайшая пара точек");
        builder.AppendLine();
        builder.AppendLine($"- Точки: случайные в квадрате [0;1]², фиксированный seed.");
        builder.AppendLine($"- n: {MinN}..{MaxN}, шаг {Step}, повторов на точку: {Runs}.");
        builder.AppendLine("- Метрика: медиана времени по повторам; прогрев перед каждым n.");
        builder.AppendLine();
        builder.AppendLine("| n | D&C, мс | D&C σ | D&C сравнений | Перебор, мс | Перебор σ | Перебор сравнений | Ускорение |");
        builder.AppendLine("|---:|---:|---:|---:|---:|---:|---:|---:|");

        foreach (var row in rows)
        {
            builder.AppendLine(
                $"| {row.N} | {row.DncMs:0.000} | {row.DncStdMs:0.000} | {row.DncComparisons:N0} | " +
                $"{row.BruteMs:0.000} | {row.BruteStdMs:0.000} | {row.BruteComparisons:N0} | ×{row.Speedup:0.0} |");
        }

        builder.AppendLine();
        builder.AppendLine($"D&C: t(n) ≈ C·n·log₂n, C = {dncC:0.####E+00} мс, MSE = {dncMse:0.####E+00}");
        builder.AppendLine($"Перебор: t(n) ≈ C·n², C = {bruteC:0.####E+00} мс, MSE = {bruteMse:0.####E+00}");
        builder.AppendLine();
        builder.AppendLine($"При n = {last.N}: D&C {last.DncMs:0.000} мс против {last.BruteMs:0.000} мс у перебора, ускорение ×{last.Speedup:0.0}.");

        File.WriteAllText(
            Path.Combine(resultsDir, "summary.md"),
            builder.ToString(),
            new UTF8Encoding(false));
    }

    private static double FitConstant(IEnumerable<double> ns, IEnumerable<double> ts, Func<double, double> model)
    {
        double numerator = 0;
        double denominator = 0;

        using var nEnumerator = ns.GetEnumerator();
        using var tEnumerator = ts.GetEnumerator();

        while (nEnumerator.MoveNext() && tEnumerator.MoveNext())
        {
            var f = model(nEnumerator.Current);
            numerator += tEnumerator.Current * f;
            denominator += f * f;
        }

        return denominator <= 0 ? 0 : numerator / denominator;
    }

    private static double Mse(IEnumerable<double> ns, IEnumerable<double> ts, double c, Func<double, double> model)
    {
        double sum = 0;
        var count = 0;

        using var nEnumerator = ns.GetEnumerator();
        using var tEnumerator = ts.GetEnumerator();

        while (nEnumerator.MoveNext() && tEnumerator.MoveNext())
        {
            var diff = tEnumerator.Current - c * model(nEnumerator.Current);
            sum += diff * diff;
            count++;
        }

        return count == 0 ? 0 : sum / count;
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "RecursiveLab.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        return Directory.GetCurrentDirectory();
    }

    private readonly record struct Measurement<T>(double MedianMs, double StdDevMs, T Last);

    private sealed record Row(
        int N,
        double DncMs,
        double DncStdMs,
        long DncComparisons,
        double BruteMs,
        double BruteStdMs,
        long BruteComparisons)
    {
        public double Speedup => DncMs <= 0 ? 0 : BruteMs / DncMs;
    }
}
