using System.Globalization;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using RecursiveLab.App.Controls;
using RecursiveLab.App.Services;
using RecursiveLab.Core.Tasks.Sudoku;

namespace RecursiveLab.App.Views.Pages;

public partial class SudokuPage : UserControl
{
    private static readonly IBrush MutedBrush = new SolidColorBrush(Color.Parse("#8FA3C8"));
    private static readonly IBrush SuccessBrush = new SolidColorBrush(Color.Parse("#34D399"));
    private static readonly IBrush WarningBrush = new SolidColorBrush(Color.Parse("#FBBF24"));
    private static readonly IBrush DangerBrush = new SolidColorBrush(Color.Parse("#F87171"));

    private readonly SudokuBoardView _boardView;
    private readonly ComboBox _presetCombo;
    private readonly TextBlock _statusText;
    private readonly TextBlock _hudFilled;
    private readonly TextBlock _hudConflicts;
    private readonly Ellipse? _hudDot;
    private readonly TextBlock _statSteps;
    private readonly TextBlock _statCalls;
    private readonly TextBlock _statTime;

    private SudokuBoard _board = new();
    private bool _initialized;

    public SudokuPage()
    {
        InitializeComponent();

        _boardView = Get<SudokuBoardView>("BoardView");
        _presetCombo = Get<ComboBox>("PresetCombo");
        _statusText = Get<TextBlock>("StatusText");
        _hudFilled = Get<TextBlock>("HudFilled");        _hudConflicts = Get<TextBlock>("HudConflicts");
        _hudDot = this.FindControl<Ellipse>("HudDot");
        _statSteps = Get<TextBlock>("StatSteps");
        _statCalls = Get<TextBlock>("StatCalls");
        _statTime = Get<TextBlock>("StatTime");

        _presetCombo.ItemsSource = SudokuPresets.All
            .Select(preset => $"{preset.Name}  ·  {preset.Difficulty}")
            .ToList();

        _presetCombo.SelectedIndex = 0;
        _presetCombo.SelectionChanged += (_, _) =>
        {
            if (_initialized)
            {
                LoadPreset(_presetCombo.SelectedIndex);
            }
        };

        _boardView.BoardChanged += OnBoardChanged;

        Get<Button>("ResetButton").Click += (_, _) => LoadPreset(_presetCombo.SelectedIndex);
        Get<Button>("CheckButton").Click += (_, _) => CheckCurrentBoard();
        Get<Button>("SolveButton").Click += OnSolveClicked;
        Get<Button>("OpenCodeButton").Click += (_, _) =>
            RepoPaths.ShowInExplorer("src/RecursiveLab.Core/Tasks/Sudoku/BacktrackingSudokuSolver.cs");

        _initialized = true;
        LoadPreset(0);
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    private void LoadPreset(int index)
    {
        if (index < 0 || index >= SudokuPresets.All.Count)
        {
            index = 0;
        }

        var preset = SudokuPresets.All[index];
        _board = SudokuBoard.Parse(preset.Board);
        _boardView.Board = _board;
        _boardView.Conflicts = _board.FindConflicts();

        ResetStats();
        UpdateHud();
        SetStatus($"Условие: {preset.Name} · {preset.Difficulty}", MutedBrush);
    }

    private void OnBoardChanged()
    {
        _boardView.Conflicts = _board.FindConflicts();
        UpdateHud();

        if (_boardView.Conflicts.Count == 0)
        {
            SetStatus($"Ручной ввод: заполнено {FilledCount()}/81, конфликтов нет", MutedBrush);
        }
        else
        {
            SetStatus($"Есть конфликты: {_boardView.Conflicts.Count}", DangerBrush);
        }
    }

    private void CheckCurrentBoard()
    {
        var conflicts = _board.FindConflicts();
        _boardView.Conflicts = conflicts;

        if (_board.IsSolved())
        {
            SetStatus("Судоку решено ✓", SuccessBrush);
        }
        else if (conflicts.Count > 0)
        {
            SetStatus($"Есть конфликты: {conflicts.Count}. Исправьте подсвеченные клетки.", DangerBrush);
        }
        else
        {
            SetStatus($"Конфликтов нет, но заполнено только {FilledCount()}/81.", WarningBrush);
        }

        UpdateHud();
    }

    private void OnSolveClicked(object? sender, RoutedEventArgs e)
    {
        SetStatus("Решаем…", MutedBrush);

        try
        {
            var solver = new BacktrackingSudokuSolver();
            var result = solver.Solve(_board.Clone());

            if (!result.Solved || result.Board is null)
            {
                SetStatus("Решатель не нашёл решения для этого условия.", WarningBrush);
                return;
            }

            _board.CopyFrom(result.Board);
            _boardView.Board = _board;
            _boardView.Conflicts = _board.FindConflicts();
            UpdateHud();

            _statSteps.Text = result.Steps.ToString("N0", CultureInfo.CurrentCulture);
            _statCalls.Text = result.RecursiveCalls.ToString("N0", CultureInfo.CurrentCulture);
            _statTime.Text = $"{result.Elapsed.TotalMilliseconds:0.0} мс";

            SetStatus(
                $"Решено за {result.Elapsed.TotalMilliseconds:0.0} мс · {result.Steps:N0} шагов",
                SuccessBrush);
        }
        catch (NotImplementedException)
        {
            ResetStats();
            SetStatus(
                "Заглушка: решатель ещё не реализован. Откройте BacktrackingSudokuSolver.cs и заполните TODO.",
                WarningBrush);
        }
        catch (Exception exception)
        {
            ResetStats();
            SetStatus($"Ошибка: {exception.Message}", DangerBrush);
        }
    }

    private void UpdateHud()
    {
        var filled = FilledCount();
        var conflicts = _boardView.Conflicts.Count;

        _hudFilled.Text = $"заполнено {filled} / 81";
        _hudConflicts.Text = conflicts == 0 ? "конфликтов нет" : $"конфликтов: {conflicts}";
        _hudConflicts.Foreground = conflicts == 0 ? MutedBrush : DangerBrush;

        if (_hudDot is not null)
        {
            _hudDot.Fill = conflicts == 0
                ? new SolidColorBrush(Color.Parse("#34D399"))
                : new SolidColorBrush(Color.Parse("#F87171"));
        }
    }

    private int FilledCount()
    {
        var count = 0;
        for (var index = 0; index < SudokuBoard.CellCount; index++)
        {
            if (_board[index] != 0)
            {
                count++;
            }
        }

        return count;
    }

    private void ResetStats()
    {
        _statSteps.Text = "—";
        _statCalls.Text = "—";
        _statTime.Text = "—";
    }

    private void SetStatus(string text, IBrush brush)
    {
        _statusText.Text = text;
        _statusText.Foreground = brush;
    }

    private T Get<T>(string name) where T : Control =>
        this.FindControl<T>(name)
        ?? throw new InvalidOperationException($"Не найден контрол '{name}' в SudokuPage.");
}
