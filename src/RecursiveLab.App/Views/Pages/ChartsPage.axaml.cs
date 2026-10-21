using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using RecursiveLab.App.Services;
using ScottPlot.Avalonia;

namespace RecursiveLab.App.Views.Pages;

public partial class ChartsPage : UserControl
{
    private readonly AvaPlot _htreePlot;
    private readonly AvaPlot _sudokuPlot;
    private readonly AvaPlot _dncPlot;
    private readonly AvaPlot _brutePlot;
    private readonly AvaPlot _hanoiPlot;
    private readonly Button _measureButton;
    private readonly TextBlock _status;
    private readonly ProgressBar _progress;

    public ChartsPage()
    {
        InitializeComponent();

        _htreePlot = Get<AvaPlot>("HtreePlot");
        _sudokuPlot = Get<AvaPlot>("SudokuPlot");
        _dncPlot = Get<AvaPlot>("DncPlot");
        _brutePlot = Get<AvaPlot>("BrutePlot");
        _hanoiPlot = Get<AvaPlot>("HanoiPlot");
        _measureButton = Get<Button>("MeasureButton");
        _status = Get<TextBlock>("BenchmarkStatus");
        _progress = Get<ProgressBar>("BenchmarkProgress");

        ConfigureFromLoadedData();

        _measureButton.Click += async (_, _) => await RunMeasurementsAsync();
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    private void ConfigureFromLoadedData()
    {
        AlgorithmPlotFactory.ConfigureHtree(_htreePlot);
        AlgorithmPlotFactory.ConfigureSudoku(_sudokuPlot);
        AlgorithmPlotFactory.ConfigureHanoi(_hanoiPlot);
        ClosestPairPlotFactory.Configure(_dncPlot, _brutePlot);
    }

    private async System.Threading.Tasks.Task RunMeasurementsAsync()
    {
        _measureButton.IsEnabled = false;
        _status.Text = "Замеряем…";
        _progress.Value = 0;

        var progress = new Progress<BenchmarkProgress>(value =>
        {
            _status.Text = $"{value.Stage}… ({value.Done}/{value.Total})";
            _progress.Value = value.Done;
        });

        try
        {
            var results = await System.Threading.Tasks.Task.Run(
                () => InAppBenchmarkRunner.Run(progress));

            AlgorithmPlotFactory.ConfigureHtree(_htreePlot, results.Htree);
            AlgorithmPlotFactory.ConfigureSudoku(_sudokuPlot, results.Sudoku);
            AlgorithmPlotFactory.ConfigureHanoi(_hanoiPlot, results.Hanoi);
            ClosestPairPlotFactory.Configure(_dncPlot, _brutePlot, results.ClosestPair);

            _progress.Value = 4;
            _status.Text = "Готово: графики обновлены";
        }
        catch (Exception exception)
        {
            _status.Text = $"Ошибка замеров: {exception.Message}";
        }
        finally
        {
            _measureButton.IsEnabled = true;
        }
    }

    private T Get<T>(string name) where T : Control =>
        this.FindControl<T>(name)
        ?? throw new InvalidOperationException($"Не найден контрол '{name}' в ChartsPage.");
}
