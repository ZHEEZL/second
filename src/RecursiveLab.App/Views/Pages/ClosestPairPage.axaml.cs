using System.Globalization;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using RecursiveLab.App.Controls;
using RecursiveLab.App.Services;
using RecursiveLab.Core.Geometry;
using RecursiveLab.Core.Tasks.ClosestPair;

namespace RecursiveLab.App.Views.Pages;

public partial class ClosestPairPage : UserControl
{
    private static readonly IBrush MutedBrush = new SolidColorBrush(Color.Parse("#8FA3C8"));
    private static readonly IBrush SuccessBrush = new SolidColorBrush(Color.Parse("#34D399"));
    private static readonly IBrush WarningBrush = new SolidColorBrush(Color.Parse("#FBBF24"));
    private static readonly IBrush DangerBrush = new SolidColorBrush(Color.Parse("#F87171"));

    private readonly PointsCanvas _pointsView;
    private readonly Slider _countSlider;
    private readonly TextBlock _countValue;
    private readonly TextBlock _hudCount;
    private readonly TextBlock _hudStatus;
    private readonly Ellipse? _hudDot;
    private readonly TextBlock _statusText;
    private readonly TextBlock _statComparisons;
    private readonly TextBlock _statCalls;
    private readonly TextBlock _statTime;

    private List<PointD> _points = new();
    private int _count = 120;
    private int _seed = 20260219;

    public ClosestPairPage()
    {
        InitializeComponent();

        _pointsView = Get<PointsCanvas>("PointsView");
        _countSlider = Get<Slider>("CountSlider");
        _countValue = Get<TextBlock>("CountValue");
        _hudCount = Get<TextBlock>("HudCount");
        _hudStatus = Get<TextBlock>("HudStatus");
        _hudDot = this.FindControl<Ellipse>("HudDot");
        _statusText = Get<TextBlock>("StatusText");
        _statComparisons = Get<TextBlock>("StatComparisons");
        _statCalls = Get<TextBlock>("StatCalls");
        _statTime = Get<TextBlock>("StatTime");

        _countSlider.ValueChanged += (_, e) =>
        {
            var count = (int)Math.Round(e.NewValue);
            if (count == _count)
            {
                return;
            }

            _count = count;
            GeneratePoints(newSeed: true);
        };

        Get<Button>("GenerateButton").Click += (_, _) => GeneratePoints(newSeed: true);
        Get<Button>("FindButton").Click += OnFindClicked;
        Get<Button>("ReferenceButton").Click += OnReferenceClicked;
        Get<Button>("OpenCodeButton").Click += (_, _) =>
            RepoPaths.ShowInExplorer("src/RecursiveLab.Core/Tasks/ClosestPair/DivideAndConquerClosestPair.cs");

        GeneratePoints(newSeed: false);
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    private void GeneratePoints(bool newSeed)
    {
        if (newSeed)
        {
            _seed = Random.Shared.Next(1, int.MaxValue);
        }

        var random = new Random(_seed);
        _points = new List<PointD>(_count);

        for (var i = 0; i < _count; i++)
        {
            _points.Add(new PointD(0.02 + random.NextDouble() * 0.96, 0.02 + random.NextDouble() * 0.96));
        }

        _pointsView.Points = _points;
        _pointsView.BestPair = null;
        _pointsView.Strip = null;
        _pointsView.SplitX = null;

        _countValue.Text = _count.ToString("N0", CultureInfo.CurrentCulture);
        _hudCount.Text = $"{_count:N0} точек · seed {_seed}";
        ResetStats();
        SetHudStatus("готово");
        SetStatus("Точки сгенерированы. Запустите алгоритм или перебор.", MutedBrush);
    }

    private void OnFindClicked(object? sender, RoutedEventArgs e)
    {
        SetHudStatus("работаем…");
        SetStatus("Запуск divide and conquer…", MutedBrush);

        try
        {
            var finder = new DivideAndConquerClosestPair();
            var result = finder.Find(_points);

            ShowResult(result);
            SetStatus(
                $"Найдена пара: {result.A} — {result.B} · d = {result.Distance:0.#####} · {result.Elapsed.TotalMilliseconds:0.0} мс",
                SuccessBrush);
            SetHudStatus("пара найдена");
            SetHudDot("#34D399");
        }
        catch (NotImplementedException)
        {
            ResetStats();
            _pointsView.BestPair = null;
            SetStatus(
                "Заглушка: divide and conquer ещё не реализован. Откройте DivideAndConquerClosestPair.cs и заполните TODO.",
                WarningBrush);
            SetHudStatus("заглушка");
            SetHudDot("#FBBF24");
        }
        catch (Exception exception)
        {
            ResetStats();
            SetStatus($"Ошибка: {exception.Message}", DangerBrush);
            SetHudStatus("ошибка");
            SetHudDot("#F87171");
        }
    }

    private void OnReferenceClicked(object? sender, RoutedEventArgs e)
    {
        SetStatus("Считаем эталон перебором…", MutedBrush);

        try
        {
            var result = ClosestPairReference.FindBruteForce(_points);
            ShowResult(result);

            SetStatus(
                $"Эталон: {result.A} — {result.B} · d = {result.Distance:0.#####} · {result.Elapsed.TotalMilliseconds:0.0} мс",
                MutedBrush);
            SetHudStatus("эталон посчитан");
            SetHudDot("#22D3EE");
        }
        catch (Exception exception)
        {
            SetStatus($"Ошибка: {exception.Message}", DangerBrush);
        }
    }

    private void ShowResult(ClosestPairResult result)
    {
        _pointsView.BestPair = result;
        _pointsView.Strip = null;
        _pointsView.SplitX = null;

        _statComparisons.Text = result.DistanceComparisons.ToString("N0", CultureInfo.CurrentCulture);
        _statCalls.Text = result.RecursiveCalls.ToString("N0", CultureInfo.CurrentCulture);
        _statTime.Text = $"{result.Elapsed.TotalMilliseconds:0.0} мс";
    }

    private void ResetStats()
    {
        _statComparisons.Text = "—";
        _statCalls.Text = "—";
        _statTime.Text = "—";
    }

    private void SetStatus(string text, IBrush brush)
    {
        _statusText.Text = text;
        _statusText.Foreground = brush;
    }

    private void SetHudStatus(string text) => _hudStatus.Text = text;

    private void SetHudDot(string hex) => _hudDot!.Fill = new SolidColorBrush(Color.Parse(hex));

    private T Get<T>(string name) where T : Control =>
        this.FindControl<T>(name)
        ?? throw new InvalidOperationException($"Не найден контрол '{name}' в ClosestPairPage.");
}
