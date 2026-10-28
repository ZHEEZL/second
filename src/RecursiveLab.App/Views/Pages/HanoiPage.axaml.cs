using System.Diagnostics;
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
using RecursiveLab.Core.Tasks.Hanoi;

namespace RecursiveLab.App.Views.Pages;

public partial class HanoiPage : UserControl
{
    private static readonly IBrush MutedBrush = new SolidColorBrush(Color.Parse("#8FA3C8"));
    private static readonly IBrush SuccessBrush = new SolidColorBrush(Color.Parse("#34D399"));
    private static readonly IBrush WarningBrush = new SolidColorBrush(Color.Parse("#FBBF24"));
    private static readonly IBrush DangerBrush = new SolidColorBrush(Color.Parse("#F87171"));

    private readonly HanoiCanvas _canvas;
    private readonly Slider _diskSlider;
    private readonly Slider _speedSlider;
    private readonly TextBlock _speedText;
    private readonly ComboBox _modeCombo;
    private readonly TextBlock _diskValue;
    private readonly TextBlock _hudDisks;
    private readonly TextBlock _hudStatus;
    private readonly TextBlock _hudMoves;
    private readonly TextBlock _statusText;
    private readonly TextBlock _lastMoveText;
    private readonly TextBlock _statExpected;
    private readonly TextBlock _statDone;
    private readonly TextBlock _statTime;
    private readonly DispatcherTimer _timer;

    private readonly List<int>[] _rods =
    [
        new(),
        new(),
        new(),
    ];

    private IReadOnlyList<HanoiMove> _moves = Array.Empty<HanoiMove>();
    private int _moveIndex;
    private int _diskCount = 4;
    private double _speed = 1;
    private bool _isPlaying;

    public HanoiPage()
    {
        InitializeComponent();


        _canvas = Get<HanoiCanvas>("TowerCanvas");
        _diskSlider = Get<Slider>("DiskSlider");
        _speedSlider = Get<Slider>("SpeedSlider");
        _speedText = Get<TextBlock>("SpeedValue");
        _modeCombo = Get<ComboBox>("ModeCombo");
        _diskValue = Get<TextBlock>("DiskValue");
        _hudDisks = Get<TextBlock>("HudDisks");
        _hudStatus = Get<TextBlock>("HudStatus");
        _hudMoves = Get<TextBlock>("HudMoves");
        _statusText = Get<TextBlock>("StatusText");
        _lastMoveText = Get<TextBlock>("LastMoveText");
        _statExpected = Get<TextBlock>("StatExpected");
        _statDone = Get<TextBlock>("StatDone");
        _statTime = Get<TextBlock>("StatTime");

        _modeCombo.ItemsSource = new[] { "Рекурсия", "Стек (доп. задание)" };
        _modeCombo.SelectedIndex = 0;
        _modeCombo.SelectionChanged += (_, _) => ResetTower();

        _diskSlider.ValueChanged += (_, e) =>
        {
            var disks = (int)Math.Round(e.NewValue);
            if (disks == _diskCount)
            {
                return;
            }

            _diskCount = Math.Clamp(disks, 3, 8);
            ResetTower();
            ApplySpeed();
        };

        _speedSlider.ValueChanged += (_, e) =>
        {
            _speed = Math.Clamp(e.NewValue, 0.25, 3);
            _speedText.Text = $"×{_speed:0.00}";
            ApplySpeed();
        };

        _speedText.Text = $"×{_speed:0.00}";

        Get<Button>("SolveButton").Click += OnSolveClicked;
        Get<Button>("ResetButton").Click += (_, _) => ResetTower();
        Get<Button>("RecursionTreeButton").Click += (_, _) => ShowRecursionTree();
        Get<Button>("OpenCodeButton").Click += (_, _) =>
        {
            var file = _modeCombo.SelectedIndex == 1
                ? "src/RecursiveLab.Core/Tasks/Hanoi/StackHanoiSolver.cs"
                : "src/RecursiveLab.Core/Tasks/Hanoi/RecursiveHanoiSolver.cs";
            RepoPaths.ShowInExplorer(file);
        };

        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(45) };
        _timer.Tick += (_, _) => OnAnimationTick();
        ApplySpeed();

        ResetTower();
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    private void ApplySpeed()
    {
        _timer.Interval = TimeSpan.FromMilliseconds(
            Math.Clamp(45 / _speed, 5, 200));
    }

    private void ResetTower()
    {
        _timer.Stop();
        _isPlaying = false;
        _moves = Array.Empty<HanoiMove>();
        _moveIndex = 0;

        foreach (var rod in _rods)
        {
            rod.Clear();
        }

        for (var disk = _diskCount; disk >= 1; disk--)
        {
            _rods[0].Add(disk);
        }

        _canvas.Rods = _rods;
        _canvas.DiskCount = _diskCount;

        _diskValue.Text = _diskCount.ToString(CultureInfo.CurrentCulture);
        _hudDisks.Text = $"{_diskCount} {DiskWord(_diskCount)}";
        _hudMoves.Text = "0 ходов";
        _statExpected.Text = ((1 << _diskCount) - 1).ToString("N0", CultureInfo.CurrentCulture);
        _statDone.Text = "0";
        _statTime.Text = "—";
        _lastMoveText.Text = "Ходы появятся после запуска алгоритма.";
        SetStatus("Алгоритм ещё не подключён", MutedBrush);
        SetHudStatus("готово");
        SetHudDot("#F472B6");
    }

    private void OnSolveClicked(object? sender, RoutedEventArgs e)
    {
        if (_isPlaying)
        {
            _timer.Stop();
            _isPlaying = false;
            return;
        }

        if (_moveIndex > 0 && _moveIndex < _moves.Count)
        {
            _isPlaying = true;
            SetHudStatus("продолжаем…");
            _timer.Start();
            return;
        }

        ResetTower();

        try
        {
            IHanoiSolver solver = _modeCombo.SelectedIndex == 1
                ? new StackHanoiSolver()
                : new RecursiveHanoiSolver();

            var stopwatch = Stopwatch.StartNew();
            _moves = solver.Solve(_diskCount);
            stopwatch.Stop();

            if (_moves.Count == 0)
            {
                SetStatus("Алгоритм вернул пустой список ходов.", WarningBrush);
                return;
            }

            _moveIndex = 0;
            _isPlaying = true;
            _statTime.Text = $"{stopwatch.Elapsed.TotalMilliseconds:0.0} мс";
            SetStatus($"Запущено · {_moves.Count:N0} ходов · {solver.Name}", SuccessBrush);
            SetHudStatus("анимация ходов");
            SetHudDot("#FBBF24");
            _timer.Start();
        }
        catch (NotImplementedException)
        {
            SetStatus(
                "Заглушка: решение ещё не реализовано. Откройте RecursiveHanoiSolver.cs (или StackHanoiSolver.cs) и заполните TODO.",
                WarningBrush);
            SetHudStatus("заглушка");
            SetHudDot("#FBBF24");
        }
        catch (Exception exception)
        {
            SetStatus($"Ошибка: {exception.Message}", DangerBrush);
            SetHudStatus("ошибка");
            SetHudDot("#F87171");
        }
    }

    private void OnAnimationTick()
    {
        if (_moveIndex >= _moves.Count)
        {
            _timer.Stop();
            _isPlaying = false;
            SetStatus("Башня перенесена ✓", SuccessBrush);
            SetHudStatus("готово");
            SetHudDot("#34D399");
            return;
        }

        var move = _moves[_moveIndex];
        var from = move.From;
        var to = move.To;

        if (from is >= 0 and < 3 && to is >= 0 and < 3 && _rods[from].Count > 0)
        {
            var disk = _rods[from][^1];
            _rods[from].RemoveAt(_rods[from].Count - 1);
            _rods[to].Add(disk);
        }

        _moveIndex++;
        _canvas.Rods = _rods;

        _statDone.Text = _moveIndex.ToString("N0", CultureInfo.CurrentCulture);
        _hudMoves.Text = $"{_moveIndex} / {_moves.Count} ходов";
        _lastMoveText.Text =
            $"Диск {move.Disk}: {(char)('A' + move.From)} → {(char)('A' + move.To)}";
    }

    private static string DiskWord(int count)
    {
        var tail = count % 10;
        var teen = count % 100 is >= 11 and <= 19;

        return tail == 1 && !teen ? "кольцо" : tail is >= 2 and <= 4 && !teen ? "кольца" : "колец";
    }

    private void SetStatus(string text, IBrush brush)
    {
        _statusText.Text = text;
        _statusText.Foreground = brush;
    }

    private void SetHudStatus(string text) => _hudStatus.Text = text;

    private void SetHudDot(string hex) =>
        this.FindControl<Ellipse>("HudDot")!.Fill = new SolidColorBrush(Color.Parse(hex));

    private void ShowRecursionTree()
    {
        try
        {
            var recorder = new RecursionTraceRecorder(
                $"Ханойские башни: {_diskCount} дисков",
                "Узел — вызов Move(n, откуда, куда). Левый ребёнок — Move(n−1) на вспомогательный стержень, " +
                "между детьми переносится диск n, правый ребёнок — Move(n−1) на целевой стержень. " +
                "Листья «0» — базовый случай. Режим «стек» обходит то же дерево: каждый кадр стека — это узел.");

            new RecursiveHanoiSolver().SolveTraced(_diskCount, recorder);
            RecursionTreeWindow.ShowFor(this, recorder.Build());
        }
        catch (Exception exception)
        {
            SetStatus($"Не удалось построить дерево: {exception.Message}", DangerBrush);
        }
    }

    private T Get<T>(string name) where T : Control =>
        this.FindControl<T>(name)
        ?? throw new InvalidOperationException($"Не найден контрол '{name}' в HanoiPage.");
}
