using ScottPlot;
using ScottPlot.Avalonia;

namespace RecursiveLab.App.Services;

/// <summary>
/// Строит графики «эксперимент + теоретическая кривая» для страницы ближайшей пары.
/// Теория: C·n·log₂n для divide and conquer и C·n² для полного перебора.
/// </summary>
public static class ClosestPairPlotFactory
{
    public static void Configure(
        AvaPlot dncPlot,
        AvaPlot brutePlot,
        IReadOnlyList<ClosestPairBenchmarkRow>? rows = null)
    {
        var data = rows ?? ClosestPairBenchmarkData.Rows;

        dncPlot.Reset(BuildPlot(data, dnc: true));
        brutePlot.Reset(BuildPlot(data, dnc: false));

        dncPlot.Refresh();
        brutePlot.Refresh();
    }

    private static Plot BuildPlot(IReadOnlyList<ClosestPairBenchmarkRow> rows, bool dnc)
    {
        var plot = new Plot();
        plot.FigureBackground.Color = Color.FromHex("#1F2229");
        plot.DataBackground.Color = Color.FromHex("#181A20");
        plot.Axes.Color(Color.FromHex("#E2E8F0"));
        plot.Grid.MajorLineColor = Color.FromHex("#2D323F");

        if (rows.Count < 2)
        {
            plot.Title("Нет данных замеров");
            var hint = plot.Add.Annotation(
                "Запустите benchmarks/RecursiveLab.Benchmarks,\nчтобы получить CSV и графики.");
            hint.LabelBackgroundColor = Color.FromHex("#262A35");
            hint.LabelBorderColor = Color.FromHex("#4B5563");
            hint.LabelFontColor = Color.FromHex("#E2E8F0");
            return plot;
        }

        Func<double, double> model = dnc
            ? n => n * Math.Log2(Math.Max(2, n))
            : n => n * n;

        var actualTimes = dnc
            ? rows.Select(row => row.DncMs).ToArray()
            : rows.Select(row => row.BruteMs).ToArray();

        var ns = rows.Select(row => (double)row.N).ToArray();
        var c = FitConstant(ns, actualTimes, model);
        var mse = Mse(ns, actualTimes, c, model);

        var actual = plot.Add.Scatter(ns, actualTimes);
        actual.LegendText = "Эксперимент";
        actual.Color = dnc ? Color.FromHex("#60A5FA") : Color.FromHex("#FB923C");
        actual.LineWidth = 2.2f;
        actual.MarkerSize = 5;

        var theoryNs = Enumerable.Range(0, 191)
            .Select(i => ns[0] + (ns[^1] - ns[0]) * i / 190.0)
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
        return plot;
    }

    private static double FitConstant(
        IReadOnlyList<double> ns,
        IReadOnlyList<double> ts,
        Func<double, double> model)
    {
        double numerator = 0;
        double denominator = 0;

        for (var i = 0; i < ns.Count; i++)
        {
            var f = model(ns[i]);
            numerator += ts[i] * f;
            denominator += f * f;
        }

        return denominator <= 0 ? 0 : numerator / denominator;
    }

    private static double Mse(
        IReadOnlyList<double> ns,
        IReadOnlyList<double> ts,
        double c,
        Func<double, double> model)
    {
        double sum = 0;

        for (var i = 0; i < ns.Count; i++)
        {
            var diff = ts[i] - c * model(ns[i]);
            sum += diff * diff;
        }

        return ns.Count == 0 ? 0 : sum / ns.Count;
    }
}
