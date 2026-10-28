using System.Globalization;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Threading;
using RecursiveLab.App.Controls;
using RecursiveLab.App.Services;
using RecursiveLab.Core.Recursion;
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
    private readonly Slider _speedSlider;
    private readonly TextBlock _countValue;
    private readonly TextBlock _speedText;
    private readonly TextBlock _hudCount;
    private readonly TextBlock _hudStatus;
    private readonly Ellipse? _hudDot;
    private readonly TextBlock _statusText;
    private readonly TextBlock _statComparisons;
    private readonly TextBlock _statCalls;
    private readonly TextBlock _statTime;
    private readonly DispatcherTimer _timer;

    private List<PointD> _points = new();
    private int _count = 120;
    private int _seed = 20260219;

    private IReadOnlyList<ClosestPairStep> _steps = Array.Empty<ClosestPairStep>();
    private int _stepIndex;
    private double _speed = 1;
    private bool _isPlaying;
    private ClosestPairResult? _result;

    public ClosestPairPage()
    {
        InitializeComponent();

        _pointsView = Get<PointsCanvas>("PointsView");
        _countSlider = Get<Slider>("CountSlider");
        _speedSlider = Get<Slider>("SpeedSlider");
        _countValue = Get<TextBlock>("CountValue");
        _speedText = Get<TextBlock>("SpeedValue");
        _hudCount = Get<TextBlock>("HudCount");
        _hudStatus = Get<TextBlock>("HudStatus");
        _hudDot = this.FindControl<Ellipse>("HudDot");
        _statusText = Get<TextBlock>("StatusText");
        _statComparisons = Get<TextBlock>("StatComparisons");
        _statCalls = Get<TextBlock>("StatCalls");
        _statTime = Get<TextBlock>("StatTime");

        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
        _timer.Tick += (_, _) => OnAnimationTick();

        _speedSlider.ValueChanged += (_, e) =>
        {
            _speed = Math.Clamp(e.NewValue, 0.25, 3);
            _speedText.Text = $"×{_speed:0.00}";
        };
        _speedText.Text = $"×{_speed:0.00}";

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
        Get<Button>("RecursionTreeButton").Click += (_, _) => ShowRecursionTree();
        Get<Button>("OpenCodeButton").Click += (_, _) =>
            RepoPaths.ShowInExplorer("src/RecursiveLab.Core/Tasks/ClosestPair/DivideAndConquerClosestPair.cs");

        GeneratePoints(newSeed: false);
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    private void GeneratePoints(bool newSeed)
    {
        StopPlayback();
        _result = null;

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
        if (_isPlaying)
        {
            StopPlayback();
            SetStatus("Анимация остановлена", WarningBrush);
            SetHudStatus("остановлено");
            return;
        }

        StopPlayback();
        SetHudStatus("работаем…");
        SetStatus("Запуск divide and conquer…", MutedBrush);

        try
        {
            var steps = new List<ClosestPairStep>();
            var finder = new DivideAndConquerClosestPair();
            var result = finder.Find(_points, step => steps.Add(step));

            _result = result;
            _statComparisons.Text = result.DistanceComparisons.ToString("N0", CultureInfo.CurrentCulture);
            _statCalls.Text = result.RecursiveCalls.ToString("N0", CultureInfo.CurrentCulture);
            _statTime.Text = $"{result.Elapsed.TotalMilliseconds:0.0} мс";

            if (steps.Count == 0)
            {
                ShowResult(result);
                SetStatus(
                    $"Найдена пара: {result.A} — {result.B} · d = {result.Distance:0.#####} · {result.Elapsed.TotalMilliseconds:0.0} мс",
                    SuccessBrush);
                SetHudStatus("пара найдена");
                SetHudDot("#34D399");
                return;
            }

            _steps = steps;
            _stepIndex = 0;
            _isPlaying = true;

            _pointsView.BestPair = null;
            _pointsView.Strip = null;
            _pointsView.SplitX = null;

            Get<Button>("FindButton").Content = "Остановить";
            SetStatus($"Анимация: {steps.Count:N0} шагов · скорость ×{_speed:0.00}", MutedBrush);
            SetHudStatus("анимация…");
            SetHudDot("#FBBF24");
            _timer.Start();
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
        StopPlayback();
        SetStatus("Считаем эталон перебором…", MutedBrush);

        try
        {
            var result = ClosestPairReference.FindBruteForce(_points);
            _result = result;
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

    private void OnAnimationTick()
    {
        if (!_isPlaying)
        {
            return;
        }

        var durationSeconds = 2.4 / _speed;
        var stepsPerTick = Math.Max(
            1,
            (int)Math.Ceiling(_steps.Count / (durationSeconds * 60.0)));

        for (var i = 0; i < stepsPerTick; i++)
        {
            if (_stepIndex >= _steps.Count)
            {
                FinishPlayback();
                return;
            }

            ApplyStep(_steps[_stepIndex++]);
        }
    }

    private void ApplyStep(ClosestPairStep step)
    {
        switch (step.Kind)
        {
            case ClosestPairStepKind.Split when double.IsFinite(step.X0):
                _pointsView.SplitX = step.X0;
                break;

            case ClosestPairStepKind.Strip when double.IsFinite(step.X0) && double.IsFinite(step.X1):
                _pointsView.Strip = (step.X0, step.X1);
                break;

            case ClosestPairStepKind.BaseCase:
            case ClosestPairStepKind.BestChanged:
                if (step.A is { } a && step.B is { } b)
                {
                    var distance = double.IsFinite(step.Distance)
                        ? step.Distance
                        : Math.Sqrt(a.DistanceSquaredTo(b));

                    _pointsView.BestPair = new ClosestPairResult(a, b, distance, 0, 0, TimeSpan.Zero);
                }

                break;
        }
    }

    private void FinishPlayback()
    {
        StopPlayback();
        _pointsView.Strip = null;
        _pointsView.SplitX = null;

        if (_result is { } result)
        {
            ShowResult(result);
            SetStatus(
                $"Готово: {result.A} — {result.B} · d = {result.Distance:0.#####} · {result.Elapsed.TotalMilliseconds:0.0} мс",
                SuccessBrush);
        }

        SetHudStatus("пара найдена");
        SetHudDot("#34D399");
    }

    private void StopPlayback()
    {
        _timer.Stop();
        _isPlaying = false;
        _steps = Array.Empty<ClosestPairStep>();
        _stepIndex = 0;

        if (this.FindControl<Button>("FindButton") is { } button)
        {
            button.Content = "Найти ближайшую пару";
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

    private void ShowRecursionTree()
    {
        try
        {
            var recorder = new RecursionTraceRecorder(
                $"Ближайшая пара: {_points.Count} точек",
                "Узел — вызов Solve(lo, hi) на отрезке массива, отсортированного по X. Каждый вызов делит отрезок " +
                "пополам и после двух рекурсивных вызовов проверяет полосу 2d. Фиолетовые листья — база: ≤ 3 точек, перебор.");

            new DivideAndConquerClosestPair().FindTraced(_points, recorder);
            RecursionTreeWindow.ShowFor(this, recorder.Build());
        }
        catch (Exception exception)
        {
            SetStatus($"Не удалось построить дерево: {exception.Message}", DangerBrush);
        }
    }

    private T Get<T>(string name) where T : Control =>
        this.FindControl<T>(name)
        ?? throw new InvalidOperationException($"Не найден контрол '{name}' в ClosestPairPage.");
}
