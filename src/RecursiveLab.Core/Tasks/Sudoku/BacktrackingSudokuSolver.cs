using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using RecursiveLab.Core.Recursion;
using RecursiveLab.Core.Tasks.Sudoku;

namespace RecursiveLab.Core.Tasks.Sudoku;

/// <summary>
/// Рекурсивный перебор с возвратом (backtracking) с эвристикой MRV.
/// </summary>
public sealed class BacktrackingSudokuSolver : ISudokuSolver
{
    public string Name => "Рекурсивный перебор с возвратом (MRV)";

    public SudokuSolveResult Solve(
        SudokuBoard board,
        Action<SudokuStep>? onStep = null,
        CancellationToken cancellationToken = default) =>
        SolveTraced(board, tracer: null, onStep, cancellationToken);

    /// <summary>
    /// То же решение, но с записью дерева рекурсии: корень — старт,
    /// каждый узел — «поставили цифру в клетку и ушли в рекурсию».
    /// </summary>
    public SudokuSolveResult SolveTraced(
        SudokuBoard board,
        IRecursionTracer? tracer,
        Action<SudokuStep>? onStep = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(board);

        // Клонируем доску, чтобы не менять исходное условие
        var work = board.Clone();
        var stopwatch = Stopwatch.StartNew();

        int steps = 0;
        long recursiveCalls = 0;

        var root = tracer?.Enter("старт", $"пустых клеток: {CountEmpty(work)}") ?? 0;

        // Запускаем рекурсию
        bool isSolved = SolveFrom(work, 0, ref steps, ref recursiveCalls, onStep, cancellationToken, tracer);

        tracer?.Exit(
            root,
            isSolved ? RecursionOutcome.Success : RecursionOutcome.DeadEnd,
            isSolved ? "судоку решено" : "решения нет");

        stopwatch.Stop();

        if (isSolved)
        {
            // Сигнализируем UI, что решение найдено
            onStep?.Invoke(new SudokuStep(0, 0, SudokuStepKind.Solved, 0));
        }

        return new SudokuSolveResult(
            isSolved,
            isSolved ? work : null,
            steps,
            recursiveCalls,
            stopwatch.Elapsed);
    }

    private bool SolveFrom(
        SudokuBoard board,
        int depth,
        ref int steps,
        ref long recursiveCalls,
        Action<SudokuStep>? onStep,
        CancellationToken cancellationToken,
        IRecursionTracer? tracer)
    {
        cancellationToken.ThrowIfCancellationRequested();
        recursiveCalls++;

        // Эвристика MRV: ищем пустую клетку с наименьшим числом возможных кандидатов
        int bestCellIndex = -1;
        int minCandidatesCount = int.MaxValue;
        IReadOnlyList<int>? bestCandidates = null;

        for (int i = 0; i < SudokuBoard.CellCount; i++)
        {
            if (board[i] == 0)
            {
                var candidates = board.GetCandidates(i);

                if (candidates.Count == 0)
                {
                    tracer?.Note($"тупик: в {CellName(i)} нечего поставить");
                    return false; // Тупик: клетка пуста, но по правилам судоку в неё нечего поставить
                }

                if (candidates.Count < minCandidatesCount)
                {
                    minCandidatesCount = candidates.Count;
                    bestCellIndex = i;
                    bestCandidates = candidates;

                    if (minCandidatesCount == 1)
                    {
                        break; // Идеальный вариант (1 кандидат), лучше не найдем, прерываем поиск
                    }
                }
            }
        }

        // Если не нашли пустых клеток (bestCellIndex остался -1), значит всё заполнено и судоку решено
        if (bestCellIndex == -1)
        {
            tracer?.Note("пустых клеток нет — решение найдено");
            return true;
        }

        // Перебираем возможных кандидатов для выбранной клетки
        foreach (var candidate in bestCandidates!)
        {
            // Подсвечиваем клетку синим в UI перед попыткой
            onStep?.Invoke(new SudokuStep(bestCellIndex, candidate, SudokuStepKind.Try, depth));

            if (board.TrySet(bestCellIndex, candidate))
            {
                steps++;
                // Рисуем цифру на доске
                onStep?.Invoke(new SudokuStep(bestCellIndex, candidate, SudokuStepKind.Place, depth));

                // Узел дерева рекурсии: «поставили цифру и ушли глубже».
                var node = tracer?.Enter(
                    $"{CellName(bestCellIndex)}={candidate}",
                    $"глубина {depth + 1}: ставим {candidate} в {CellName(bestCellIndex)}; " +
                    $"кандидаты клетки: {string.Join(", ", bestCandidates)}") ?? 0;

                // Уходим в рекурсию
                var solved = SolveFrom(board, depth + 1, ref steps, ref recursiveCalls, onStep, cancellationToken, tracer);

                tracer?.Exit(
                    node,
                    solved ? RecursionOutcome.Success : RecursionOutcome.DeadEnd,
                    solved ? "эта ветка ведёт к решению" : $"ветка не подошла — стираем {candidate} (откат)");

                if (solved)
                {
                    return true; // Решение найдено, раскручиваем стек
                }

                // Откат (backtracking): если путь привёл в тупик, стираем значение
                board.TryClear(bestCellIndex);
                // Убираем цифру с доски в UI
                onStep?.Invoke(new SudokuStep(bestCellIndex, 0, SudokuStepKind.Remove, depth));
            }
        }

        tracer?.Note($"ни один кандидат для {CellName(bestCellIndex)} не подошёл — возврат");
        return false;
    }

    /// <summary>Имя клетки в виде r3c5 (строка 3, столбец 5, с единицы).</summary>
    public static string CellName(int index)
    {
        var (row, column) = SudokuBoard.ToRowColumn(index);
        return $"r{row + 1}c{column + 1}";
    }

    private static int CountEmpty(SudokuBoard board)
    {
        var count = 0;
        for (var i = 0; i < SudokuBoard.CellCount; i++)
        {
            if (board[i] == 0)
            {
                count++;
            }
        }

        return count;
    }
}
