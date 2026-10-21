using System.Diagnostics;
using System.Globalization;
using System.Text;
using RecursiveLab.Core.Tasks.Hanoi;
using RecursiveLab.Core.Tasks.HTree;
using RecursiveLab.Core.Tasks.Sudoku;
using ScottPlot;

namespace RecursiveLab.Benchmarks;

/// <summary>
/// Замеры и графики «эксперимент + теоретическая кривая» для H-дерева,
/// судоку и Ханойских башен. Ближайшая пара измеряется в Program.cs.
/// </summary>
internal static class OtherBenchmarks
{
    public static void Run(string resultsDir, string chartDir)
    {
        RunHtree(resultsDir, chartDir);
        RunHanoi(resultsDir, chartDir);
        RunSudoku(resultsDir, chartDir);
    }

    private static void RunHtree(string resultsDir, string chartDir)
    {
        var rows = new List<HtreeRow>();
        Console.WriteLine("\n=== H-дерево: время генерации ===");

        for (var depth = 0; depth <= HTreeGenerator.MaxDepth - 1; depth++)
        {
            var segments = HTreeGenerator.Generate(1.0, depth);
            var batch = depth switch
            {
                <= 2 => 2000,
                <= 4 => 300,
                _ => 30,
            };

            var ms = Measure(() => HTreeGenerator.Generate(1.0, depth), batch, 7);
            rows.Add(new HtreeRow(depth, segments.Count, ms));
            Console.WriteLine($"d={depth}  отрезков={segments.Count,6}  t={ms,9:0.0000} мс");
        }

        var model = (Func<double, double>)(d => Math.Pow(4, d));
        var c = FitConstant(rows.Select(row => (double)row.Depth), rows.Select(row => row.Ms), model);

        var csv = new StringBuilder("depth;segments;actual_ms;theory_ms\n");
        foreach (var row in rows)
        {
            var theoryMs = c * model(row.Depth);
            csv.AppendLine(string.Join(';',
                row.Depth.ToString(CultureInfo.InvariantCulture),
                row.Segments.ToString(CultureInfo.InvariantCulture),
                row.Ms.ToString("0.######", CultureInfo.InvariantCulture),
                theoryMs.ToString("0.######", CultureInfo.InvariantCulture)));
        }

        File.WriteAllText(Path.Combine(resultsDir, "htree.csv"), csv.ToString(), new UTF8Encoding(false));

        var plot = NewPlot();
        var xs = rows.Select(row => (double)row.Depth).ToArray();
        var actual = plot.Add.Scatter(xs, rows.Select(row => row.Ms).ToArray());
        actual.LegendText = "Эксперимент";
        actual.Color = Color.FromHex("#60A5FA");
        actual.LineWidth = 2.2f;
        actual.MarkerSize = 6;

        var theoryXs = Enumerable.Range(0, 130).Select(i => i * 6.0 / 129.0).ToArray();
        var theory = plot.Add.Scatter(theoryXs, theoryXs.Select(x => c * model(x)).ToArray());
        theory.LegendText = "Теория: C · 4^d";
        theory.Color = Color.FromHex("#C084FC");
        theory.LineWidth = 2.0f;
        theory.MarkerSize = 0;
        theory.LinePattern = LinePattern.Dashed;

        AddAnnotation(plot, $"C = {c:0.####E+00} мс");
        plot.Title("H-дерево: время генерации и теория O(4^d)");
        plot.XLabel("Глубина рекурсии d");
        plot.YLabel("Время, мс");
        plot.ShowLegend(Alignment.UpperLeft);
        plot.Axes.AutoScale();
        plot.SavePng(Path.Combine(chartDir, "htree-theory.png"), 1000, 620);
    }

    private static void RunHanoi(string resultsDir, string chartDir)
    {
        var rows = new List<HanoiRow>();
        var solver = new RecursiveHanoiSolver();
        Console.WriteLine("\n=== Ханойские башни: время решения ===");

        for (var disks = 3; disks <= 12; disks++)
        {
            var moves = solver.Solve(disks);
            var ms = Measure(() => solver.Solve(disks), batch: 1, runs: 5);
            rows.Add(new HanoiRow(disks, moves.Count, ms));
            Console.WriteLine($"n={disks,2}  ходов={moves.Count,5}  t={ms,9:0.0000} мс");
        }

        var model = (Func<double, double>)(n => Math.Pow(2, n));
        var c = FitConstant(rows.Select(row => (double)row.Disks), rows.Select(row => row.Ms), model);

        var csv = new StringBuilder("disks;moves;actual_ms;theory_ms\n");
        foreach (var row in rows)
        {
            var theoryMs = c * model(row.Disks);
            csv.AppendLine(string.Join(';',
                row.Disks.ToString(CultureInfo.InvariantCulture),
                row.Moves.ToString(CultureInfo.InvariantCulture),
                row.Ms.ToString("0.######", CultureInfo.InvariantCulture),
                theoryMs.ToString("0.######", CultureInfo.InvariantCulture)));
        }

        File.WriteAllText(Path.Combine(resultsDir, "hanoi.csv"), csv.ToString(), new UTF8Encoding(false));

        var plot = NewPlot();
        var xs = rows.Select(row => (double)row.Disks).ToArray();
        var actual = plot.Add.Scatter(xs, rows.Select(row => row.Ms).ToArray());
        actual.LegendText = "Эксперимент";
        actual.Color = Color.FromHex("#F472B6");
        actual.LineWidth = 2.2f;
        actual.MarkerSize = 6;

        var theoryXs = Enumerable.Range(0, 130).Select(i => 3.0 + i * 9.0 / 129.0).ToArray();
        var theory = plot.Add.Scatter(theoryXs, theoryXs.Select(x => c * model(x)).ToArray());
        theory.LegendText = "Теория: C · 2^n";
        theory.Color = Color.FromHex("#C084FC");
        theory.LineWidth = 2.0f;
        theory.MarkerSize = 0;
        theory.LinePattern = LinePattern.Dashed;

        AddAnnotation(plot, $"C = {c:0.####E+00} мс");
        plot.Title("Ханойские башни: время решения и теория O(2^n)");
        plot.XLabel("Количество дисков n");
        plot.YLabel("Время, мс");
        plot.ShowLegend(Alignment.UpperLeft);
        plot.Axes.AutoScale();
        plot.SavePng(Path.Combine(chartDir, "hanoi-theory.png"), 1000, 620);
    }

    private static void RunSudoku(string resultsDir, string chartDir)
    {
        const string solvedText =
            "534678912" + "672195348" + "198342567" +
            "859761423" + "426853791" + "713924856" +
            "961537284" + "287419635" + "345286179";

        var solved = SudokuBoard.Parse(solvedText);
        var solver = new BacktrackingSudokuSolver();
        var random = new Random(20260219);
        var empties = new[] { 20, 30, 40, 50, 60 };
        var rows = new List<SudokuRow>();

        Console.WriteLine("\n=== Судоку: время решения ===");

        foreach (var emptyCount in empties)
        {
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
                Console.WriteLine($"m={emptyCount}: решения нет, пропуск");
                continue;
            }

            var ms = Measure(() => solver.Solve(board), batch: 1, runs: 5);
            rows.Add(new SudokuRow(emptyCount, ms, result.Steps, result.RecursiveCalls));
            Console.WriteLine(
                $"пусто={emptyCount,2}  t={ms,9:0.0000} мс  шагов={result.Steps,6}  вызовов={result.RecursiveCalls,7}");
        }

        // Верхняя оценка O(9^m) для MRV-решателя слишком пессимистична:
        // она на десятки порядков выше реального времени. Поэтому на графике
        // показываем эмпирическую модель t ≈ C·a^m, подобранную по замерам.
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

        var csv = new StringBuilder("empties;actual_ms;steps;calls;empirical_ms\n");
        foreach (var row in rows)
        {
            var empirical = modelC * Math.Pow(modelA, row.Empties);
            csv.AppendLine(string.Join(';',
                row.Empties.ToString(CultureInfo.InvariantCulture),
                row.Ms.ToString("0.######", CultureInfo.InvariantCulture),
                row.Steps.ToString(CultureInfo.InvariantCulture),
                row.Calls.ToString(CultureInfo.InvariantCulture),
                empirical.ToString("0.######", CultureInfo.InvariantCulture)));
        }

        File.WriteAllText(Path.Combine(resultsDir, "sudoku.csv"), csv.ToString(), new UTF8Encoding(false));

        var plot = NewPlot();
        var xs = rows.Select(row => (double)row.Empties).ToArray();
        var actual = plot.Add.Scatter(xs, rows.Select(row => row.Ms).ToArray());
        actual.LegendText = "Эксперимент";
        actual.Color = Color.FromHex("#A78BFA");
        actual.LineWidth = 2.2f;
        actual.MarkerSize = 7;

        var modelXs = Enumerable.Range(0, 130).Select(i => 20.0 + i * 40.0 / 129.0).ToArray();
        var model = plot.Add.Scatter(
            modelXs,
            modelXs.Select(m => modelC * Math.Pow(modelA, m)).ToArray());
        model.LegendText = "Эмпирическая модель: C · a^m";
        model.Color = Color.FromHex("#C084FC");
        model.LineWidth = 2.0f;
        model.MarkerSize = 0;
        model.LinePattern = LinePattern.Dashed;

        AddAnnotation(plot, $"C = {modelC:0.####E+00} мс, a = {modelA:0.####}", Alignment.UpperRight);
        AddAnnotation(plot, "m — пустые клетки; больше m — больше перебор", Alignment.LowerRight);
        plot.Title("Судоку: время решения и эмпирическая модель");
        plot.XLabel("Количество пустых клеток m");
        plot.YLabel("Время, мс");
        plot.ShowLegend(Alignment.UpperLeft);
        plot.Axes.AutoScale();
        plot.SavePng(Path.Combine(chartDir, "sudoku-theory.png"), 1000, 620);
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

    private static Plot NewPlot()
    {
        var plot = new Plot();
        plot.FigureBackground.Color = Color.FromHex("#1F2229");
        plot.DataBackground.Color = Color.FromHex("#181A20");
        plot.Axes.Color(Color.FromHex("#E2E8F0"));
        plot.Grid.MajorLineColor = Color.FromHex("#2D323F");
        return plot;
    }

    private static void AddAnnotation(Plot plot, string text, Alignment alignment = Alignment.UpperLeft)
    {
        var annotation = plot.Add.Annotation(text, alignment);
        annotation.LabelBackgroundColor = Color.FromHex("#262A35");
        annotation.LabelBorderColor = Color.FromHex("#4B5563");
        annotation.LabelFontColor = Color.FromHex("#E2E8F0");
        annotation.LabelFontSize = 11;
    }

    private static double FitConstant(IEnumerable<double> xs, IEnumerable<double> ys, Func<double, double> model)
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

    private readonly record struct HtreeRow(int Depth, int Segments, double Ms);

    private readonly record struct HanoiRow(int Disks, int Moves, double Ms);

    private readonly record struct SudokuRow(int Empties, double Ms, int Steps, long Calls);
}
