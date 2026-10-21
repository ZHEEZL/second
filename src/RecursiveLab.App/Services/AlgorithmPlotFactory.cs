using ScottPlot;
using ScottPlot.Avalonia;

namespace RecursiveLab.App.Services;

/// <summary>Графики «эксперимент + модель» для H-дерева, судоку и Ханоя.</summary>
public static class AlgorithmPlotFactory
{
    private const string SudokuNote = "m — пустые клетки; больше m — больше перебор";

    public static void ConfigureHtree(AvaPlot plot, IReadOnlyList<ChartPoint>? data = null) =>
        Configure(
            plot,
            data ?? AlgorithmBenchmarkData.Htree,
            "H-дерево: время генерации и теория O(4^d)",
            "Глубина рекурсии d",
            "Время, мс",
            "Теория: C · 4^d");

    public static void ConfigureHanoi(AvaPlot plot, IReadOnlyList<ChartPoint>? data = null) =>
        Configure(
            plot,
            data ?? AlgorithmBenchmarkData.Hanoi,
            "Ханойские башни: время решения и теория O(2^n)",
            "Количество дисков n",
            "Время, мс",
            "Теория: C · 2^n");

    public static void ConfigureSudoku(AvaPlot plot, IReadOnlyList<ChartPoint>? data = null) =>
        Configure(
            plot,
            data ?? AlgorithmBenchmarkData.Sudoku,
            "Судоку: время решения и эмпирическая модель",
            "Количество пустых клеток m",
            "Время, мс",
            "Эмпирическая модель: C · a^m",
            SudokuNote);

    private static void Configure(
        AvaPlot avaPlot,
        IReadOnlyList<ChartPoint> data,
        string title,
        string xLabel,
        string yLabel,
        string theoryLabel,
        string? note = null)
    {
        var plot = new Plot();
        plot.FigureBackground.Color = Color.FromHex("#1F2229");
        plot.DataBackground.Color = Color.FromHex("#181A20");
        plot.Axes.Color(Color.FromHex("#E2E8F0"));
        plot.Grid.MajorLineColor = Color.FromHex("#2D323F");
        plot.Title(title);
        plot.XLabel(xLabel);
        plot.YLabel(yLabel);
        plot.ShowLegend(Alignment.UpperLeft);

        if (note is not null)
        {
            var annotation = plot.Add.Annotation(note, Alignment.LowerRight);
            annotation.LabelBackgroundColor = Color.FromHex("#262A35");
            annotation.LabelBorderColor = Color.FromHex("#4B5563");
            annotation.LabelFontColor = Color.FromHex("#E2E8F0");
            annotation.LabelFontSize = 11;
        }

        if (data.Count < 2)
        {
            var hint = plot.Add.Annotation(
                "Нет данных. Запустите benchmarks/RecursiveLab.Benchmarks.");
            hint.LabelBackgroundColor = Color.FromHex("#262A35");
            hint.LabelBorderColor = Color.FromHex("#4B5563");
            hint.LabelFontColor = Color.FromHex("#E2E8F0");
        }
        else
        {
            var xs = data.Select(point => point.X).ToArray();

            var actual = plot.Add.Scatter(xs, data.Select(point => point.Actual).ToArray());
            actual.LegendText = "Эксперимент";
            actual.Color = Color.FromHex("#60A5FA");
            actual.LineWidth = 2.2f;
            actual.MarkerSize = 6;

            var theory = plot.Add.Scatter(xs, data.Select(point => point.Theory).ToArray());
            theory.LegendText = theoryLabel;
            theory.Color = Color.FromHex("#C084FC");
            theory.LineWidth = 2.0f;
            theory.MarkerSize = 0;
            theory.LinePattern = LinePattern.Dashed;

            plot.Axes.AutoScale();
        }

        avaPlot.Reset(plot);
        avaPlot.Refresh();
    }
}
