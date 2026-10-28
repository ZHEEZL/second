using System.Diagnostics;
using RecursiveLab.Core.Recursion;

namespace RecursiveLab.Core.Tasks.Sudoku;

/// <summary>
/// Генератор судоку в три шага:
/// 1) рекурсивно строит полную случайную сетку (перебор с возвратом, цифры в случайном порядке);
/// 2) убирает клетки равномерно по блокам 3×3, после каждого удаления проверяя,
///    что решение осталось единственным (рекурсивный подсчёт с лимитом 2);
/// 3) если цель по числу подсказок не достигнута — пробует заново и оставляет лучший вариант.
/// </summary>
public sealed class RandomizedSudokuGenerator : ISudokuGenerator
{
    public string Name => "Рекурсивная генерация с проверкой единственности";

    public SudokuPuzzleResult Generate(
        SudokuGenerationOptions options,
        Action<int>? onAttempt = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentOutOfRangeException.ThrowIfLessThan(options.TargetClues, SudokuDifficultyScale.MinClues, nameof(options.TargetClues));
        ArgumentOutOfRangeException.ThrowIfGreaterThan(options.TargetClues, SudokuDifficultyScale.MaxClues, nameof(options.TargetClues));
        ArgumentOutOfRangeException.ThrowIfLessThan(options.MaxAttempts, 1, nameof(options.MaxAttempts));

        var seed = options.Seed ?? Random.Shared.Next();
        var random = new Random(seed); // Свой Random на каждый вызов: Random не потокобезопасен.
        var stopwatch = Stopwatch.StartNew();
        long recursiveCalls = 0;

        SudokuBoard? bestPuzzle = null;
        SudokuBoard? bestSolution = null;
        RecursionTree? bestFillTree = null;
        var bestClues = int.MaxValue;
        var attempts = 0;

        for (var attempt = 1; attempt <= options.MaxAttempts; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            attempts = attempt;
            onAttempt?.Invoke(attempt);

            // Дерево рекурсии записываем только по просьбе: на случайность это не влияет,
            // задача с тем же seed получится та же самая.
            var recorder = options.TraceRecursion
                ? new RecursionTraceRecorder(
                    $"Построение сетки (Fill), попытка {attempt}",
                    "Узел — шаг рекурсии Fill: в первую пустую клетку ставится цифра (кандидаты перемешаны), " +
                    "дальше вызов для следующей клетки. Красные ветки — тупики с откатом.")
                : null;

            var solution = CreateSolvedGrid(random, ref recursiveCalls, cancellationToken, recorder);
            var (puzzle, clues) = RemoveCells(solution, options.TargetClues, random, ref recursiveCalls, cancellationToken);

            if (clues < bestClues)
            {
                bestPuzzle = puzzle;
                bestSolution = solution;
                bestClues = clues;
                bestFillTree = recorder?.Build();
            }

            if (clues <= options.TargetClues)
            {
                break; // Цель достигнута, дальше пробовать не нужно.
            }
        }

        // Оставшиеся цифры становятся «стартовыми»: их нельзя стереть в UI, и PDF рисует их жирным.
        MarkRemainingAsGivens(bestPuzzle!);

        var solutionCount = SudokuSolutionCounter.CountSolutions(bestPuzzle!, 2, out var finalCalls, cancellationToken);
        recursiveCalls += finalCalls;
        stopwatch.Stop();

        return new SudokuPuzzleResult(
            bestPuzzle!,
            bestSolution!,
            options.TargetClues,
            bestClues,
            solutionCount,
            attempts,
            seed,
            recursiveCalls,
            stopwatch.Elapsed)
        {
            FillTree = bestFillTree,
        };
    }

    /// <summary>Строит полную правильную случайную сетку 9×9.</summary>
    public static SudokuBoard CreateSolvedGrid(
        Random random,
        CancellationToken cancellationToken = default,
        IRecursionTracer? tracer = null)
    {
        long calls = 0;
        return CreateSolvedGrid(random, ref calls, cancellationToken, tracer);
    }

    /// <summary>Перемешивание Фишера–Йетса: все перестановки равновероятны.</summary>
    public static void Shuffle<T>(IList<T> items, Random random)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(random);

        for (var i = items.Count - 1; i > 0; i--)
        {
            var j = random.Next(i + 1); // 0..i включительно
            (items[i], items[j]) = (items[j], items[i]);
        }
    }

    /// <summary>
    /// Порядок обхода 81 клетки «по кругу по блокам»: в каждом круге берётся
    /// по одной случайной клетке из каждого блока 3×3. Так дыры распределяются
    /// по доске равномерно, а не скапливаются в одном углу.
    /// </summary>
    public static IReadOnlyList<int> BoxRoundRobinOrder(Random random)
    {
        ArgumentNullException.ThrowIfNull(random);

        var boxes = new List<int>[SudokuBoard.Size];
        for (var box = 0; box < SudokuBoard.Size; box++)
        {
            boxes[box] = new List<int>(SudokuBoard.Size);
        }

        for (var index = 0; index < SudokuBoard.CellCount; index++)
        {
            boxes[BoxOf(index)].Add(index);
        }

        foreach (var cells in boxes)
        {
            Shuffle(cells, random);
        }

        var order = new List<int>(SudokuBoard.CellCount);
        var boxOrder = Enumerable.Range(0, SudokuBoard.Size).ToList();

        for (var round = 0; round < SudokuBoard.Size; round++)
        {
            Shuffle(boxOrder, random);
            foreach (var box in boxOrder)
            {
                order.Add(boxes[box][round]);
            }
        }

        return order;
    }

    /// <summary>Номер блока 3×3 (0..8) для клетки.</summary>
    public static int BoxOf(int index)
    {
        var (row, column) = SudokuBoard.ToRowColumn(index);
        return row / 3 * 3 + column / 3;
    }

    private static SudokuBoard CreateSolvedGrid(
        Random random,
        ref long recursiveCalls,
        CancellationToken cancellationToken,
        IRecursionTracer? tracer)
    {
        ArgumentNullException.ThrowIfNull(random);

        // Клетки обычные (не «стартовые»), иначе потом их нельзя было бы стереть через TryClear.
        var board = new SudokuBoard();

        var root = tracer?.Enter("пустая доска", "Fill: заполняем 81 клетку по порядку, цифры пробуем в случайном порядке") ?? 0;
        var filled = Fill(board, random, ref recursiveCalls, cancellationToken, tracer);
        tracer?.Exit(root, filled ? RecursionOutcome.Success : RecursionOutcome.DeadEnd, "сетка построена");

        if (!filled)
        {
            // На пустой доске решение есть всегда, сюда попасть нельзя.
            throw new InvalidOperationException("Не удалось построить сетку судоку.");
        }

        return board;
    }

    /// <summary>Рекурсивное заполнение: первая пустая клетка, кандидаты в случайном порядке, откат при тупике.</summary>
    private static bool Fill(
        SudokuBoard board,
        Random random,
        ref long recursiveCalls,
        CancellationToken cancellationToken,
        IRecursionTracer? tracer)
    {
        cancellationToken.ThrowIfCancellationRequested();
        recursiveCalls++;

        var cell = FirstEmptyCell(board);
        if (cell == -1)
        {
            tracer?.Note("все 81 клетка заполнены");
            return true; // Базовый случай: всё заполнено.
        }

        // GetCandidates возвращает IReadOnlyList — перемешиваем копию.
        var candidates = new List<int>(board.GetCandidates(cell));
        Shuffle(candidates, random);

        if (candidates.Count == 0)
        {
            tracer?.Note($"тупик: в {BacktrackingSudokuSolver.CellName(cell)} нечего поставить");
        }

        foreach (var value in candidates)
        {
            board.TrySet(cell, value);

            var node = tracer?.Enter(
                $"{BacktrackingSudokuSolver.CellName(cell)}={value}",
                $"ставим {value} в {BacktrackingSudokuSolver.CellName(cell)}; " +
                $"порядок после перемешивания: {string.Join(", ", candidates)}") ?? 0;

            var filled = Fill(board, random, ref recursiveCalls, cancellationToken, tracer);

            tracer?.Exit(
                node,
                filled ? RecursionOutcome.Success : RecursionOutcome.DeadEnd,
                filled ? "дальше удалось заполнить всё" : $"тупик — стираем {value} (откат)");

            if (filled)
            {
                return true; // Рекурсивный шаг удался.
            }

            board.TryClear(cell); // Откат.
        }

        return false; // Тупик: ни одна цифра не подошла.
    }

    private static int FirstEmptyCell(SudokuBoard board)
    {
        for (var index = 0; index < SudokuBoard.CellCount; index++)
        {
            if (board[index] == 0)
            {
                return index;
            }
        }

        return -1;
    }

    /// <summary>
    /// Жадное удаление: идём по клеткам в порядке «по кругу по блокам»,
    /// стираем клетку и оставляем её пустой, только если решение осталось единственным.
    /// Одного прохода достаточно: если клетку нельзя убрать сейчас, позже —
    /// когда подсказок станет меньше — её тем более нельзя убрать.
    /// </summary>
    private static (SudokuBoard Puzzle, int Clues) RemoveCells(
        SudokuBoard solution,
        int targetClues,
        Random random,
        ref long recursiveCalls,
        CancellationToken cancellationToken)
    {
        var puzzle = solution.Clone();
        var clues = SudokuBoard.CellCount;

        foreach (var cell in BoxRoundRobinOrder(random))
        {
            if (clues <= targetClues)
            {
                break;
            }

            var value = puzzle[cell];
            puzzle.TryClear(cell);

            var count = SudokuSolutionCounter.CountSolutions(puzzle, 2, out var calls, cancellationToken);
            recursiveCalls += calls;

            if (count == 1)
            {
                clues--;
            }
            else
            {
                puzzle.TrySet(cell, value); // Решений стало больше одного — возвращаем цифру.
            }
        }

        return (puzzle, clues);
    }

    private static void MarkRemainingAsGivens(SudokuBoard puzzle)
    {
        for (var index = 0; index < SudokuBoard.CellCount; index++)
        {
            if (puzzle[index] != 0)
            {
                puzzle.SetGiven(index, puzzle[index]);
            }
        }
    }
}
