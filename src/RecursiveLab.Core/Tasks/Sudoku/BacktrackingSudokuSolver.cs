using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
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
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(board);

        // Клонируем доску, чтобы не менять исходное условие
        var work = board.Clone();
        var stopwatch = Stopwatch.StartNew();

        int steps = 0;
        long recursiveCalls = 0;

        // Запускаем рекурсию
        bool isSolved = SolveFrom(work, 0, ref steps, ref recursiveCalls, onStep, cancellationToken);

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
        CancellationToken cancellationToken)
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

                // Уходим в рекурсию
                if (SolveFrom(board, depth + 1, ref steps, ref recursiveCalls, onStep, cancellationToken))
                {
                    return true; // Решение найдено, раскручиваем стек
                }

                // Откат (backtracking): если путь привёл в тупик, стираем значение
                board.TryClear(bestCellIndex);
                // Убираем цифру с доски в UI
                onStep?.Invoke(new SudokuStep(bestCellIndex, 0, SudokuStepKind.Remove, depth));
            }
        }

        return false;
    }
}
