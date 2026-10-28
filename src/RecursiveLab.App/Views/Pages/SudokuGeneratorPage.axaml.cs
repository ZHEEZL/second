using System.Globalization;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using RecursiveLab.App.Controls;
using RecursiveLab.App.Services;
using RecursiveLab.Core.Recursion;
using RecursiveLab.Core.Tasks.Sudoku;

namespace RecursiveLab.App.Views.Pages;

/// <summary>
/// Вкладка «Генератор судоку»: выбор сложности или числа подсказок,
/// генерация в фоновом потоке с отменой, проверка ввода, экспорт в PDF.
/// Сам алгоритм — в Core (RandomizedSudokuGenerator, SudokuSolutionCounter).
/// </summary>
public partial class SudokuGeneratorPage : UserControl
{
    private const int DefaultMaxAttempts = 20;
    private const int LowCluesWarningThreshold = 24;

    private static readonly IBrush MutedBrush = new SolidColorBrush(Color.Parse("#8FA3C8"));
    private static readonly IBrush SuccessBrush = new SolidColorBrush(Color.Parse("#34D399"));
    private static readonly IBrush WarningBrush = new SolidColorBrush(Color.Parse("#FBBF24"));
    private static readonly IBrush DangerBrush = new SolidColorBrush(Color.Parse("#F87171"));

    private readonly ISudokuGenerator _generator = new RandomizedSudokuGenerator();

    private readonly SudokuBoardView _boardView;
    private readonly ComboBox _difficultyCombo;
    private readonly NumericUpDown _cluesInput;
    private readonly TextBlock _removalText;
    private readonly TextBlock _cluesWarning;
    private readonly TextBox _seedInput;
    private readonly Button _generateButton;
    private readonly Button _cancelButton;
    private readonly Button _checkButton;
    private readonly Button _solutionButton;
    private readonly Button _resetButton;
    private readonly Button _savePdfButton;
    private readonly CheckBox _answersCheck;
    private readonly TextBlock _statusText;
    private readonly TextBlock _hudClues;
    private readonly TextBlock _hudSolutions;
    private readonly Ellipse? _hudDot;
    private readonly TextBlock _statClues;
    private readonly TextBlock _statSolutions;
    private readonly TextBlock _statAttempts;
    private readonly TextBlock _statCalls;
    private readonly TextBlock _statTime;
    private readonly TextBlock _statSeed;

    private CancellationTokenSource? _cts;
    private int _runId;
    private SudokuPuzzleResult? _result;
    private string _resultDifficulty = "";
    private bool _showingSolution;
    private bool _syncingInputs;

    public SudokuGeneratorPage()
    {
        InitializeComponent();

        _boardView = Get<SudokuBoardView>("BoardView");
        _difficultyCombo = Get<ComboBox>("DifficultyCombo");
        _cluesInput = Get<NumericUpDown>("CluesInput");
        _removalText = Get<TextBlock>("RemovalText");
        _cluesWarning = Get<TextBlock>("CluesWarning");
        _seedInput = Get<TextBox>("SeedInput");
        _generateButton = Get<Button>("GenerateButton");
        _cancelButton = Get<Button>("CancelButton");
        _checkButton = Get<Button>("CheckButton");
        _solutionButton = Get<Button>("SolutionButton");
        _resetButton = Get<Button>("ResetButton");
        _savePdfButton = Get<Button>("SavePdfButton");
        _answersCheck = Get<CheckBox>("AnswersCheck");
        _statusText = Get<TextBlock>("StatusText");
        _hudClues = Get<TextBlock>("HudClues");
        _hudSolutions = Get<TextBlock>("HudSolutions");
        _hudDot = this.FindControl<Ellipse>("HudDot");
        _statClues = Get<TextBlock>("StatClues");
        _statSolutions = Get<TextBlock>("StatSolutions");
        _statAttempts = Get<TextBlock>("StatAttempts");
        _statCalls = Get<TextBlock>("StatCalls");
        _statTime = Get<TextBlock>("StatTime");
        _statSeed = Get<TextBlock>("StatSeed");

        // Пункты: четыре уровня сложности + ручной режим.
        var items = SudokuDifficultyScale.All
            .Select(d =>
                $"{SudokuDifficultyScale.DisplayName(d)} · {SudokuDifficultyScale.ToTargetClues(d)} подсказок " +
                $"(убрать {SudokuDifficultyScale.RemovedShare(d) * 100:0}%)")
            .Append("Своё число подсказок")
            .ToList();

        _difficultyCombo.ItemsSource = items;
        _difficultyCombo.SelectionChanged += (_, _) => OnDifficultyChanged();
        _cluesInput.ValueChanged += (_, _) => OnCluesChanged();

        _generateButton.Click += (_, _) => _ = GenerateAsync();
        _cancelButton.Click += (_, _) => _cts?.Cancel();
        _checkButton.Click += (_, _) => CheckInput();
        _solutionButton.Click += (_, _) => ToggleSolution();
        _resetButton.Click += (_, _) => ResetInput();
        _savePdfButton.Click += (_, _) => _ = SavePdfAsync();
        _boardView.BoardChanged += UpdateHud;

        Get<Button>("RecursionTreeButton").Click += (_, _) => ShowRecursionTree();
        Get<Button>("OpenCodeButton").Click += (_, _) =>
            RepoPaths.ShowInExplorer("src/RecursiveLab.Core/Tasks/Sudoku/RandomizedSudokuGenerator.cs");

        _difficultyCombo.SelectedIndex = 1; // «Средняя» по умолчанию.
        UpdateResultButtons();

        // Первая задача создаётся, когда вкладку впервые показали (не в конструкторе —
        // конструктор не должен делать долгую работу).
        Loaded += (_, _) =>
        {
            if (_result is null && _cts is null)
            {
                _ = GenerateAsync();
            }
        };
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    // ---------- Параметры ----------

    private bool IsCustomSelected => _difficultyCombo.SelectedIndex == SudokuDifficultyScale.All.Count;

    private SudokuDifficulty? SelectedDifficulty =>
        _difficultyCombo.SelectedIndex >= 0 && _difficultyCombo.SelectedIndex < SudokuDifficultyScale.All.Count
            ? SudokuDifficultyScale.All[_difficultyCombo.SelectedIndex]
            : null;

    private void OnDifficultyChanged()
    {
        if (SelectedDifficulty is { } difficulty)
        {
            _syncingInputs = true;
            _cluesInput.Value = SudokuDifficultyScale.ToTargetClues(difficulty);
            _syncingInputs = false;
        }

        UpdateRemovalText();
    }

    private void OnCluesChanged()
    {
        if (!_syncingInputs && SelectedDifficulty is { } difficulty &&
            ReadClues() != SudokuDifficultyScale.ToTargetClues(difficulty))
        {
            // Пользователь поменял число руками — это уже «своё число».
            _syncingInputs = true;
            _difficultyCombo.SelectedIndex = SudokuDifficultyScale.All.Count;
            _syncingInputs = false;
        }

        UpdateRemovalText();
    }

    private int ReadClues()
    {
        var value = (int)Math.Round(_cluesInput.Value ?? 36m);
        return Math.Clamp(value, SudokuDifficultyScale.MinClues, SudokuDifficultyScale.MaxClues);
    }

    private void UpdateRemovalText()
    {
        var clues = ReadClues();
        var removed = SudokuBoard.CellCount - clues;
        var percent = removed * 100.0 / SudokuBoard.CellCount;

        _removalText.Text = $"убрать {removed} из 81 клеток ({percent:0}%)";
        _cluesWarning.IsVisible = clues < LowCluesWarningThreshold;
    }

    private string CurrentDifficultyLabel() =>
        SelectedDifficulty is { } difficulty
            ? SudokuDifficultyScale.DisplayName(difficulty)
            : $"своя ({ReadClues()} подсказок)";

    private bool TryReadSeed(out int? seed)
    {
        seed = null;
        var text = _seedInput.Text?.Trim();

        if (string.IsNullOrEmpty(text))
        {
            return true;
        }

        if (int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
        {
            seed = value;
            return true;
        }

        return false;
    }

    // ---------- Генерация ----------

    private async Task GenerateAsync()
    {
        if (_cts is not null)
        {
            return; // Уже идёт генерация.
        }

        // Все контролы читаем ДО Task.Run: из фонового потока их трогать нельзя.
        if (!TryReadSeed(out var seed))
        {
            SetStatus("Seed должен быть целым числом (или оставьте поле пустым).", DangerBrush);
            return;
        }

        var options = new SudokuGenerationOptions(ReadClues(), seed, DefaultMaxAttempts, TraceRecursion: true);
        var difficultyLabel = CurrentDifficultyLabel();

        var runId = ++_runId;
        var cts = new CancellationTokenSource();
        _cts = cts;
        var token = cts.Token;

        SetBusy(true);
        SetStatus($"Генерируем задачу на {options.TargetClues} подсказок…", MutedBrush);

        try
        {
            var result = await Task.Run(
                () => _generator.Generate(
                    options,
                    attempt => Dispatcher.UIThread.Post(() =>
                    {
                        // Сообщение могло опоздать: генерация уже закончилась или началась новая.
                        if (_cts is not null && runId == _runId)
                        {
                            SetStatus($"Попытка {attempt} из {options.MaxAttempts}…", MutedBrush);
                        }
                    }),
                    token),
                token);

            // После await мы снова в потоке интерфейса — можно обновлять контролы.
            ShowResult(result, difficultyLabel);
        }
        catch (OperationCanceledException)
        {
            SetStatus("Генерация отменена.", WarningBrush);
        }
        catch (Exception exception)
        {
            SetStatus($"Ошибка: {exception.Message}", DangerBrush);
        }
        finally
        {
            cts.Dispose();
            _cts = null;
            SetBusy(false);
        }
    }

    private void ShowResult(SudokuPuzzleResult result, string difficultyLabel)
    {
        _result = result;
        _resultDifficulty = difficultyLabel;
        _showingSolution = false;
        _solutionButton.Content = "Показать ответ";

        // Даём доске копию: пользователь может вписывать цифры, а исходная задача
        // (для PDF и проверки) должна остаться нетронутой.
        _boardView.Board = result.Puzzle.Clone();

        _statClues.Text = result.ActualClues.ToString(CultureInfo.CurrentCulture);
        _statSolutions.Text = result.SolutionCount == 1 ? "1" : $"{result.SolutionCount}+";
        _statAttempts.Text = result.Attempts.ToString(CultureInfo.CurrentCulture);
        _statCalls.Text = result.RecursiveCalls.ToString("N0", CultureInfo.CurrentCulture);
        _statTime.Text = $"{result.Elapsed.TotalMilliseconds:0} мс";
        _statSeed.Text = result.Seed.ToString(CultureInfo.InvariantCulture);

        if (result.SolutionCount != 1)
        {
            SetStatus($"Ошибка генератора: у задачи {result.SolutionCount}+ решений.", DangerBrush);
        }
        else if (result.ReachedTarget)
        {
            SetStatus(
                $"Готово: {result.ActualClues} подсказок ({difficultyLabel}), решение единственное ✓",
                SuccessBrush);
        }
        else
        {
            SetStatus(
                $"Запрошено {result.RequestedClues} подсказок, удалось оставить {result.ActualClues}: " +
                $"меньше без потери единственности не вышло за {result.Attempts} попыток. Это нормально — " +
                "минимум для судоку 17, а случайным удалением обычно получается 22–27.",
                WarningBrush);
        }

        UpdateHud();
        UpdateResultButtons();
    }

    private void SetBusy(bool busy)
    {
        _generateButton.IsEnabled = !busy;
        _cancelButton.IsEnabled = busy;
        _difficultyCombo.IsEnabled = !busy;
        _cluesInput.IsEnabled = !busy;
        _seedInput.IsEnabled = !busy;
        _generateButton.Content = busy ? "Генерируем…" : "Сгенерировать";
        UpdateResultButtons();
    }

    private void UpdateResultButtons()
    {
        var ready = _result is not null && _cts is null;
        _checkButton.IsEnabled = ready;
        _solutionButton.IsEnabled = ready;
        _resetButton.IsEnabled = ready;
        _savePdfButton.IsEnabled = ready;
        Get<Button>("RecursionTreeButton").IsEnabled = ready;
    }

    // ---------- Работа с доской ----------

    private void CheckInput()
    {
        if (_result is null)
        {
            return;
        }

        var board = _boardView.Board;
        var wrong = new List<int>();
        var filled = 0;

        for (var index = 0; index < SudokuBoard.CellCount; index++)
        {
            if (board[index] == 0)
            {
                continue;
            }

            filled++;
            if (board[index] != _result.Solution[index])
            {
                wrong.Add(index);
            }
        }

        _boardView.Conflicts = wrong;

        if (wrong.Count > 0)
        {
            SetStatus($"Неверных цифр: {wrong.Count}. Они подсвечены красным.", DangerBrush);
        }
        else if (filled == SudokuBoard.CellCount)
        {
            SetStatus("Судоку решено верно ✓", SuccessBrush);
        }
        else
        {
            SetStatus($"Ошибок нет, заполнено {filled}/81.", WarningBrush);
        }
    }

    private void ToggleSolution()
    {
        if (_result is null)
        {
            return;
        }

        _showingSolution = !_showingSolution;

        if (_showingSolution)
        {
            // Подсказки остаются «стартовыми», ответы вписываются обычными цифрами —
            // доска покажет их другим цветом.
            var view = _result.Puzzle.Clone();
            for (var index = 0; index < SudokuBoard.CellCount; index++)
            {
                if (view[index] == 0)
                {
                    view.TrySet(index, _result.Solution[index]);
                }
            }

            _boardView.Board = view;
            _solutionButton.Content = "Скрыть ответ";
        }
        else
        {
            _boardView.Board = _result.Puzzle.Clone();
            _solutionButton.Content = "Показать ответ";
        }

        UpdateHud();
    }

    private void ResetInput()
    {
        if (_result is null)
        {
            return;
        }

        _showingSolution = false;
        _solutionButton.Content = "Показать ответ";
        _boardView.Board = _result.Puzzle.Clone();
        SetStatus("Ввод стёрт, на доске снова исходная задача.", MutedBrush);
        UpdateHud();
    }

    private void UpdateHud()
    {
        if (_result is null)
        {
            _hudClues.Text = "подсказок — / 81";
            _hudSolutions.Text = "задача ещё не создана";
            SetHudDot(MutedBrush);
            return;
        }

        var filled = 0;
        for (var index = 0; index < SudokuBoard.CellCount; index++)
        {
            if (_boardView.Board[index] != 0)
            {
                filled++;
            }
        }

        _hudClues.Text = $"подсказок {_result.ActualClues} / 81 · заполнено {filled}";
        _hudSolutions.Text = _result.SolutionCount == 1
            ? "решение единственное"
            : $"решений: {_result.SolutionCount}+";
        SetHudDot(_result.SolutionCount == 1 ? SuccessBrush : DangerBrush);
    }

    private void SetHudDot(IBrush brush)
    {
        if (_hudDot is not null)
        {
            _hudDot.Fill = brush;
        }
    }

    // ---------- PDF ----------

    private async Task SavePdfAsync()
    {
        if (_result is null)
        {
            return;
        }

        var result = _result;
        var options = SudokuPdfOptions.For(result, _resultDifficulty, _answersCheck.IsChecked == true);

        try
        {
            var storage = TopLevel.GetTopLevel(this)?.StorageProvider;
            if (storage is null || !storage.CanSave)
            {
                SetStatus("Сохранение файлов недоступно на этой платформе.", DangerBrush);
                return;
            }

            var file = await storage.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Сохранить судоку в PDF",
                SuggestedFileName = $"sudoku-{result.ActualClues}-{result.Seed}.pdf",
                DefaultExtension = "pdf",
                FileTypeChoices =
                [
                    new FilePickerFileType("PDF")
                    {
                        Patterns = ["*.pdf"],
                        MimeTypes = ["application/pdf"],
                    },
                ],
            });

            if (file is null)
            {
                return; // Пользователь нажал «Отмена».
            }

            var fileName = file.Name;

            using (file)
            {
                await using var stream = await file.OpenWriteAsync();
                if (stream.CanSeek)
                {
                    stream.SetLength(0); // Если файл уже был — перезаписываем целиком.
                }

                SudokuPdfExporter.Export(result, stream, options);
            }

            SetStatus($"PDF сохранён: {fileName}", SuccessBrush);
        }
        catch (Exception exception)
        {
            SetStatus($"Не удалось сохранить PDF: {exception.Message}", DangerBrush);
        }
    }

    // ---------- Вспомогательное ----------

    private void SetStatus(string text, IBrush brush)
    {
        _statusText.Text = text;
        _statusText.Foreground = brush;
    }

    private void ShowRecursionTree()
    {
        if (_result is null)
        {
            SetStatus("Сначала сгенерируйте задачу.", WarningBrush);
            return;
        }

        try
        {
            var trees = new List<RecursionTree>();
            if (_result.FillTree is not null)
            {
                trees.Add(_result.FillTree);
            }

            var recorder = new RecursionTraceRecorder(
                "Проверка единственности: CountSolutions(задача, лимит 2)",
                "Корень — готовая задача. Узел — «поставили цифру в клетку (MRV) и посчитали решения дальше». " +
                "Зелёная ветка дошла до решения, красные — тупики. У хорошей задачи до листа доходит ровно одна ветка.");

            SudokuSolutionCounter.CountSolutions(_result.Puzzle, 2, out _, default, recorder);
            trees.Add(recorder.Build());

            RecursionTreeWindow.ShowFor(this, trees.ToArray());
        }
        catch (Exception exception)
        {
            SetStatus($"Не удалось построить дерево: {exception.Message}", DangerBrush);
        }
    }

    private T Get<T>(string name) where T : Control =>
        this.FindControl<T>(name)
        ?? throw new InvalidOperationException($"Не найден контрол '{name}' в SudokuGeneratorPage.");
}
