using System.Diagnostics;
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
/// генерация подборки задач в фоновом потоке с отменой, проверка ввода,
/// экспорт всей подборки в один PDF (несколько задач на листе).
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
    private readonly NumericUpDown _countInput;
    private readonly TextBlock _removalText;
    private readonly TextBlock _cluesWarning;
    private readonly TextBox _seedInput;
    private readonly Button _generateButton;
    private readonly Button _cancelButton;
    private readonly Button _checkButton;
    private readonly Button _solutionButton;
    private readonly Button _resetButton;
    private readonly Button _savePdfButton;
    private readonly Button _showFileButton;
    private readonly Button _recursionTreeButton;
    private readonly Button _prevButton;
    private readonly Button _nextButton;
    private readonly StackPanel _batchNavigation;
    private readonly TextBlock _batchText;
    private readonly ComboBox _perPageCombo;
    private readonly CheckBox _answersCheck;
    private readonly TextBlock _paperText;
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

    // Подборка: все сгенерированные задачи и номер той, что сейчас на доске.
    private List<SudokuPuzzleResult> _batch = new();
    private int _index;
    private string _batchDifficulty = "";
    private string? _lastSavedPath;

    private bool _showingSolution;
    private bool _syncingInputs;

    public SudokuGeneratorPage()
    {
        InitializeComponent();

        _boardView = Get<SudokuBoardView>("BoardView");
        _difficultyCombo = Get<ComboBox>("DifficultyCombo");
        _cluesInput = Get<NumericUpDown>("CluesInput");
        _countInput = Get<NumericUpDown>("CountInput");
        _removalText = Get<TextBlock>("RemovalText");
        _cluesWarning = Get<TextBlock>("CluesWarning");
        _seedInput = Get<TextBox>("SeedInput");
        _generateButton = Get<Button>("GenerateButton");
        _cancelButton = Get<Button>("CancelButton");
        _checkButton = Get<Button>("CheckButton");
        _solutionButton = Get<Button>("SolutionButton");
        _resetButton = Get<Button>("ResetButton");
        _savePdfButton = Get<Button>("SavePdfButton");
        _showFileButton = Get<Button>("ShowFileButton");
        _recursionTreeButton = Get<Button>("RecursionTreeButton");
        _prevButton = Get<Button>("PrevButton");
        _nextButton = Get<Button>("NextButton");
        _batchNavigation = Get<StackPanel>("BatchNavigation");
        _batchText = Get<TextBlock>("BatchText");
        _perPageCombo = Get<ComboBox>("PerPageCombo");
        _answersCheck = Get<CheckBox>("AnswersCheck");
        _paperText = Get<TextBlock>("PaperText");
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
        _countInput.ValueChanged += (_, _) => UpdatePaperText();

        _perPageCombo.ItemsSource = SudokuPdfExporter.SupportedPerPage
            .Select(n => n switch
            {
                1 => "1 (крупно)",
                2 => "2",
                4 => "4",
                _ => $"{n} (экономно)",
            })
            .ToList();
        _perPageCombo.SelectedIndex = 2; // по 4 на листе
        _perPageCombo.SelectionChanged += (_, _) => UpdatePaperText();
        _answersCheck.IsCheckedChanged += (_, _) => UpdatePaperText();

        _generateButton.Click += (_, _) => _ = GenerateAsync();
        _cancelButton.Click += (_, _) => _cts?.Cancel();
        _checkButton.Click += (_, _) => CheckInput();
        _solutionButton.Click += (_, _) => ToggleSolution();
        _resetButton.Click += (_, _) => ResetInput();
        _savePdfButton.Click += (_, _) => _ = SavePdfAsync();
        _showFileButton.Click += (_, _) => ShowSavedFile();
        _prevButton.Click += (_, _) => ShowPuzzle(_index - 1);
        _nextButton.Click += (_, _) => ShowPuzzle(_index + 1);
        _boardView.BoardChanged += UpdateHud;

        _recursionTreeButton.Click += (_, _) => ShowRecursionTree();
        Get<Button>("OpenCodeButton").Click += (_, _) =>
            RepoPaths.ShowInExplorer("src/RecursiveLab.Core/Tasks/Sudoku/RandomizedSudokuGenerator.cs");

        _difficultyCombo.SelectedIndex = 1; // «Средняя» по умолчанию.
        UpdateResultButtons();
        UpdatePaperText();

        // Первая задача создаётся, когда вкладку впервые показали (не в конструкторе —
        // конструктор не должен делать долгую работу).
        Loaded += (_, _) =>
        {
            if (_batch.Count == 0 && _cts is null)
            {
                _ = GenerateAsync();
            }
        };
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    /// <summary>Задача, которая сейчас на доске.</summary>
    private SudokuPuzzleResult? Current => _batch.Count == 0 ? null : _batch[_index];

    // ---------- Параметры ----------

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

    private int ReadCount()
    {
        var value = (int)Math.Round(_countInput.Value ?? 1m);
        return Math.Clamp(value, 1, 100);
    }

    private int ReadPerPage()
    {
        var index = _perPageCombo.SelectedIndex;
        return index >= 0 && index < SudokuPdfExporter.SupportedPerPage.Count
            ? SudokuPdfExporter.SupportedPerPage[index]
            : 4;
    }

    private void UpdateRemovalText()
    {
        var clues = ReadClues();
        var removed = SudokuBoard.CellCount - clues;
        var percent = removed * 100.0 / SudokuBoard.CellCount;

        _removalText.Text = $"убрать {removed} из 81 клеток ({percent:0}%)";
        _cluesWarning.IsVisible = clues < LowCluesWarningThreshold;
    }

    /// <summary>Сколько бумаги займёт подборка — считаем для того, что уже сгенерировано (или будет).</summary>
    private void UpdatePaperText()
    {
        var count = _batch.Count > 0 ? _batch.Count : ReadCount();
        var (puzzlePages, answerPages) = SudokuPdfExporter.EstimatePages(count, ReadPerPage(), _answersCheck.IsChecked == true);

        var text = $"{count} {SudokuPdfOptions.Plural(count, "задача", "задачи", "задач")} → " +
                   $"{puzzlePages} {SudokuPdfOptions.Plural(puzzlePages, "лист", "листа", "листов")}";
        if (answerPages > 0)
        {
            text += $" + {answerPages} с ответами";
        }

        _paperText.Text = text;
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

        var targetClues = ReadClues();
        var count = ReadCount();
        var difficultyLabel = CurrentDifficultyLabel();

        // Одно зерно на всю подборку: задача № k получает seed + k − 1,
        // поэтому подборку можно повторить, зная только первое число.
        var baseSeed = seed ?? Random.Shared.Next(0, int.MaxValue - 1000);

        var runId = ++_runId;
        var cts = new CancellationTokenSource();
        _cts = cts;
        var token = cts.Token;

        SetBusy(true);
        SetStatus(count == 1
            ? $"Генерируем задачу на {targetClues} подсказок…"
            : $"Генерируем {count} задач по {targetClues} подсказок…", MutedBrush);

        // Сюда фоновый поток складывает готовые задачи: при отмене уже готовые не теряются.
        var produced = new List<SudokuPuzzleResult>(count);

        try
        {
            await Task.Run(
                () =>
                {
                    for (var i = 0; i < count; i++)
                    {
                        token.ThrowIfCancellationRequested();
                        var number = i + 1;

                        var options = new SudokuGenerationOptions(
                            targetClues,
                            unchecked(baseSeed + i),
                            DefaultMaxAttempts,
                            TraceRecursion: true);

                        var result = _generator.Generate(
                            options,
                            attempt => Dispatcher.UIThread.Post(() =>
                            {
                                // Сообщение могло опоздать: генерация уже закончилась или началась новая.
                                if (_cts is not null && runId == _runId)
                                {
                                    SetStatus(count == 1
                                        ? $"Попытка {attempt} из {DefaultMaxAttempts}…"
                                        : $"Задача {number} из {count}, попытка {attempt}…", MutedBrush);
                                }
                            }),
                            token);

                        lock (produced)
                        {
                            produced.Add(result);
                        }
                    }
                },
                token);

            // После await мы снова в потоке интерфейса — можно обновлять контролы.
            ShowBatch(produced, difficultyLabel, cancelled: false);
        }
        catch (OperationCanceledException)
        {
            if (produced.Count > 0)
            {
                ShowBatch(produced, difficultyLabel, cancelled: true);
            }
            else
            {
                SetStatus("Генерация отменена.", WarningBrush);
            }
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

    private void ShowBatch(List<SudokuPuzzleResult> batch, string difficultyLabel, bool cancelled)
    {
        _batch = batch;
        _batchDifficulty = difficultyLabel;
        _lastSavedPath = null;
        _showFileButton.IsEnabled = false;

        ShowPuzzle(0);
        UpdatePaperText();

        var notUnique = batch.Count(p => p.SolutionCount != 1);
        var notReached = batch.Count(p => !p.ReachedTarget);

        if (notUnique > 0)
        {
            SetStatus($"Ошибка генератора: у {notUnique} задач решение не единственное.", DangerBrush);
            return;
        }

        if (batch.Count == 1 && !cancelled)
        {
            var only = batch[0];
            if (only.ReachedTarget)
            {
                SetStatus($"Готово: {only.ActualClues} подсказок ({difficultyLabel}), решение единственное ✓", SuccessBrush);
            }
            else
            {
                SetStatus(
                    $"Запрошено {only.RequestedClues} подсказок, удалось оставить {only.ActualClues}: " +
                    $"меньше без потери единственности не вышло за {only.Attempts} попыток. Это нормально — " +
                    "минимум для судоку 17, а случайным удалением обычно получается 22–27.",
                    WarningBrush);
            }

            return;
        }

        var minClues = batch.Min(p => p.ActualClues);
        var maxClues = batch.Max(p => p.ActualClues);
        var clues = minClues == maxClues ? $"{minClues}" : $"от {minClues} до {maxClues}";
        var head = cancelled
            ? $"Отменено, но {batch.Count} задач уже готовы"
            : $"Готово: {batch.Count} задач";
        var text = $"{head} ({difficultyLabel}), подсказок {clues}, у каждой решение единственное ✓";

        if (notReached > 0)
        {
            text += $" В {notReached} задачах подсказок осталось больше запрошенного — меньше без потери единственности не вышло.";
        }

        SetStatus(text, cancelled || notReached > 0 ? WarningBrush : SuccessBrush);
    }

    /// <summary>Показать на доске задачу № index из подборки.</summary>
    private void ShowPuzzle(int index)
    {
        if (_batch.Count == 0)
        {
            return;
        }

        _index = Math.Clamp(index, 0, _batch.Count - 1);
        var result = _batch[_index];

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

        _batchNavigation.IsVisible = _batch.Count > 1;
        _batchText.Text = $"задача {_index + 1} из {_batch.Count}";
        _prevButton.IsEnabled = _index > 0;
        _nextButton.IsEnabled = _index < _batch.Count - 1;

        UpdateHud();
        UpdateResultButtons();
    }

    private void SetBusy(bool busy)
    {
        _generateButton.IsEnabled = !busy;
        _cancelButton.IsEnabled = busy;
        _difficultyCombo.IsEnabled = !busy;
        _cluesInput.IsEnabled = !busy;
        _countInput.IsEnabled = !busy;
        _seedInput.IsEnabled = !busy;
        _generateButton.Content = busy ? "Генерируем…" : "Сгенерировать";
        UpdateResultButtons();
    }

    private void UpdateResultButtons()
    {
        var ready = _batch.Count > 0 && _cts is null;
        _checkButton.IsEnabled = ready;
        _solutionButton.IsEnabled = ready;
        _resetButton.IsEnabled = ready;
        _savePdfButton.IsEnabled = ready;
        _recursionTreeButton.IsEnabled = ready;
        _prevButton.IsEnabled = ready && _index > 0;
        _nextButton.IsEnabled = ready && _index < _batch.Count - 1;
    }

    // ---------- Работа с доской ----------

    private void CheckInput()
    {
        if (Current is not { } result)
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
            if (board[index] != result.Solution[index])
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
        if (Current is not { } result)
        {
            return;
        }

        _showingSolution = !_showingSolution;

        if (_showingSolution)
        {
            // Подсказки остаются «стартовыми», ответы вписываются обычными цифрами —
            // доска покажет их другим цветом.
            var view = result.Puzzle.Clone();
            for (var index = 0; index < SudokuBoard.CellCount; index++)
            {
                if (view[index] == 0)
                {
                    view.TrySet(index, result.Solution[index]);
                }
            }

            _boardView.Board = view;
            _solutionButton.Content = "Скрыть ответ";
        }
        else
        {
            _boardView.Board = result.Puzzle.Clone();
            _solutionButton.Content = "Показать ответ";
        }

        UpdateHud();
    }

    private void ResetInput()
    {
        if (Current is not { } result)
        {
            return;
        }

        _showingSolution = false;
        _solutionButton.Content = "Показать ответ";
        _boardView.Board = result.Puzzle.Clone();
        SetStatus("Ввод стёрт, на доске снова исходная задача.", MutedBrush);
        UpdateHud();
    }

    private void UpdateHud()
    {
        if (Current is not { } result)
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

        _hudClues.Text = $"подсказок {result.ActualClues} / 81 · заполнено {filled}";
        _hudSolutions.Text = result.SolutionCount == 1
            ? "решение единственное"
            : $"решений: {result.SolutionCount}+";
        SetHudDot(result.SolutionCount == 1 ? SuccessBrush : DangerBrush);
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
        if (_batch.Count == 0)
        {
            return;
        }

        var batch = _batch.ToList();
        var options = SudokuPdfOptions.ForCollection(batch, _batchDifficulty, _answersCheck.IsChecked == true, ReadPerPage());
        var suggestedName = batch.Count == 1
            ? $"sudoku-{batch[0].ActualClues}-{batch[0].Seed}.pdf"
            : $"sudoku-{batch.Count}-zadach-{batch[0].Seed}.pdf";

        // 1. Сначала собираем PDF в памяти: так ошибки рисования и ошибки записи на диск не путаются.
        byte[] pdf;
        try
        {
            using var memory = new MemoryStream();
            SudokuPdfExporter.Export(batch, memory, options);
            pdf = memory.ToArray();
        }
        catch (Exception exception)
        {
            SetStatus($"Не удалось построить PDF: {exception.Message}", DangerBrush);
            return;
        }

        // 2. Спрашиваем, куда сохранить.
        string? preferredPath = null;
        Func<Task<Stream>>? openStream = null;
        IStorageFile? file = null;

        try
        {
            var storage = TopLevel.GetTopLevel(this)?.StorageProvider;
            if (storage is not null && storage.CanSave)
            {
                file = await storage.SaveFilePickerAsync(new FilePickerSaveOptions
                {
                    Title = "Сохранить судоку в PDF",
                    SuggestedFileName = suggestedName,
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

                // Пишем сами по обычному пути — так понятнее ошибки, чем через поток диалога.
                preferredPath = file.TryGetLocalPath();
                var picked = file;
                openStream = () => picked.OpenWriteAsync();
                suggestedName = file.Name;
            }
        }
        catch (Exception exception)
        {
            // Диалог не открылся — всё равно сохраним в запасную папку ниже.
            SetStatus($"Диалог сохранения не открылся: {exception.Message}", WarningBrush);
        }

        // 3. Записываем; если Windows не пускает в выбранную папку — сохраняем в «Загрузки».
        PdfFileWriter.SaveOutcome outcome;
        try
        {
            outcome = await PdfFileWriter.WriteAsync(pdf, preferredPath, openStream, suggestedName);
        }
        catch (Exception exception)
        {
            SetStatus($"Не удалось сохранить PDF: {exception.Message}", DangerBrush);
            return;
        }
        finally
        {
            file?.Dispose();
        }

        if (outcome.SavedPath is null)
        {
            SetStatus($"Не удалось сохранить PDF: {outcome.Problem}. Попробуйте выбрать другую папку.", DangerBrush);
            return;
        }

        _lastSavedPath = File.Exists(outcome.SavedPath) ? outcome.SavedPath : null;
        _showFileButton.IsEnabled = _lastSavedPath is not null;

        var pages = SudokuPdfExporter.EstimatePages(batch.Count, ReadPerPage(), _answersCheck.IsChecked == true);
        var summary = $"{batch.Count} {SudokuPdfOptions.Plural(batch.Count, "задача", "задачи", "задач")}, " +
                      $"{pages.PuzzlePages + pages.AnswerPages} стр.";

        if (outcome.UsedFallback)
        {
            SetStatus(
                $"В выбранное место записать не получилось: {outcome.Problem}. " +
                $"PDF ({summary}) сохранён сюда: {outcome.SavedPath}",
                WarningBrush);
        }
        else
        {
            SetStatus($"PDF сохранён ({summary}): {outcome.SavedPath}", SuccessBrush);
        }
    }

    private void ShowSavedFile()
    {
        if (_lastSavedPath is null || !File.Exists(_lastSavedPath))
        {
            return;
        }

        try
        {
            if (OperatingSystem.IsWindows())
            {
                Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{_lastSavedPath}\"") { UseShellExecute = true });
            }
            else
            {
                Process.Start(new ProcessStartInfo(_lastSavedPath) { UseShellExecute = true });
            }
        }
        catch
        {
            // Открыть проводник — не критично.
        }
    }

    // ---------- Дерево рекурсии ----------

    private void ShowRecursionTree()
    {
        if (Current is not { } result)
        {
            SetStatus("Сначала сгенерируйте задачу.", WarningBrush);
            return;
        }

        try
        {
            var trees = new List<RecursionTree>();
            if (result.FillTree is not null)
            {
                trees.Add(result.FillTree);
            }

            var recorder = new RecursionTraceRecorder(
                $"Проверка единственности задачи № {_index + 1}: CountSolutions(задача, лимит 2)",
                "Корень — готовая задача. Узел — «поставили цифру в клетку (MRV) и посчитали решения дальше». " +
                "Зелёная ветка дошла до решения, красные — тупики. У хорошей задачи до листа доходит ровно одна ветка.");

            SudokuSolutionCounter.CountSolutions(result.Puzzle, 2, out _, default, recorder);
            trees.Add(recorder.Build());

            RecursionTreeWindow.ShowFor(this, trees.ToArray());
        }
        catch (Exception exception)
        {
            SetStatus($"Не удалось построить дерево: {exception.Message}", DangerBrush);
        }
    }

    // ---------- Вспомогательное ----------

    private void SetStatus(string text, IBrush brush)
    {
        _statusText.Text = text;
        _statusText.Foreground = brush;
    }

    private T Get<T>(string name) where T : Control =>
        this.FindControl<T>(name)
        ?? throw new InvalidOperationException($"Не найден контрол '{name}' в SudokuGeneratorPage.");
}
