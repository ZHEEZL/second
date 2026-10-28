using RecursiveLab.Core.Recursion;

namespace RecursiveLab.Core.Tasks.Sudoku;

/// <summary>
/// Рекурсивный подсчёт решений судоку с ограничением сверху (limit).
/// Нужен генератору: задача хорошая, только если решение ровно одно.
/// </summary>
public static class SudokuSolutionCounter
{
    /// <summary>
    /// Считает решения, но не больше <paramref name="limit"/>.
    /// 0 — решений нет, 1 — решение единственное, limit — «limit или больше».
    /// Исходная доска не изменяется.
    /// </summary>
    public static int CountSolutions(
        SudokuBoard board,
        int limit = 2,
        CancellationToken cancellationToken = default) =>
        CountSolutions(board, limit, out _, cancellationToken);

    /// <summary>
    /// То же самое, плюс число рекурсивных вызовов (для статистики).
    /// Если передан <paramref name="tracer"/>, строится дерево рекурсии:
    /// корень — задача, узел — «поставили цифру в клетку и ушли глубже».
    /// </summary>
    public static int CountSolutions(
        SudokuBoard board,
        int limit,
        out long recursiveCalls,
        CancellationToken cancellationToken = default,
        IRecursionTracer? tracer = null)
    {
        ArgumentNullException.ThrowIfNull(board);
        ArgumentOutOfRangeException.ThrowIfLessThan(limit, 1);

        recursiveCalls = 0;

        // Если в условии уже есть повтор, решений нет. Проверяем заранее:
        // GetCandidates смотрит только на пустые клетки и такой конфликт
        // не заметит — поиск пошёл бы перебирать огромное пространство зря.
        if (board.FindConflicts().Count > 0)
        {
            var conflictNode = tracer?.Enter("задача", "в условии есть повтор цифр") ?? 0;
            tracer?.Exit(conflictNode, RecursionOutcome.DeadEnd, "решений 0: условие противоречиво");
            return 0;
        }

        var work = board.Clone();
        var count = 0;

        var root = tracer?.Enter("задача", $"ищем решения, лимит {limit}") ?? 0;
        Search(work, limit, ref count, ref recursiveCalls, cancellationToken, tracer);
        tracer?.Exit(
            root,
            count > 0 ? RecursionOutcome.Success : RecursionOutcome.DeadEnd,
            count >= limit ? $"найдено {count} (лимит) — решение не единственное" : $"решений: {count}");

        return count;
    }

    /// <summary>Есть ли у задачи ровно одно решение.</summary>
    public static bool HasUniqueSolution(SudokuBoard board, CancellationToken cancellationToken = default) =>
        CountSolutions(board, 2, cancellationToken) == 1;

    private static void Search(
        SudokuBoard board,
        int limit,
        ref int count,
        ref long recursiveCalls,
        CancellationToken cancellationToken,
        IRecursionTracer? tracer)
    {
        cancellationToken.ThrowIfCancellationRequested();
        recursiveCalls++;

        // MRV: пустая клетка с наименьшим числом кандидатов (как в BacktrackingSudokuSolver).
        var bestCell = -1;
        IReadOnlyList<int>? bestCandidates = null;

        for (var index = 0; index < SudokuBoard.CellCount; index++)
        {
            if (board[index] != 0)
            {
                continue;
            }

            var candidates = board.GetCandidates(index);

            if (candidates.Count == 0)
            {
                tracer?.Note($"тупик: в {BacktrackingSudokuSolver.CellName(index)} нечего поставить");
                return; // Базовый случай 2: тупик, в эту клетку ставить нечего.
            }

            if (bestCandidates is null || candidates.Count < bestCandidates.Count)
            {
                bestCell = index;
                bestCandidates = candidates;

                if (candidates.Count == 1)
                {
                    break; // Лучше одного кандидата не бывает.
                }
            }
        }

        if (bestCell == -1)
        {
            count++; // Базовый случай 1: пустых клеток нет — нашли решение.
            tracer?.Note($"решение №{count}");
            return;
        }

        foreach (var value in bestCandidates!)
        {
            var before = count;
            board.TrySet(bestCell, value);

            var node = tracer?.Enter(
                $"{BacktrackingSudokuSolver.CellName(bestCell)}={value}",
                $"ставим {value} в {BacktrackingSudokuSolver.CellName(bestCell)}; " +
                $"кандидаты: {string.Join(", ", bestCandidates)}") ?? 0;

            Search(board, limit, ref count, ref recursiveCalls, cancellationToken, tracer); // Рекурсивный шаг.

            tracer?.Exit(
                node,
                count > before ? RecursionOutcome.Success : RecursionOutcome.DeadEnd,
                count > before ? $"в этой ветке решений: {count - before}" : "решений нет — откат");

            board.TryClear(bestCell); // Откат — всегда, даже если сейчас выйдем.

            if (count >= limit)
            {
                tracer?.Note($"достигнут лимит {limit} — дальше не ищем");
                return; // Базовый случай 3: дальше искать незачем.
            }
        }
    }
}
