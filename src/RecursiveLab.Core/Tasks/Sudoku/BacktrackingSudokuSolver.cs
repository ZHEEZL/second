namespace RecursiveLab.Core.Tasks.Sudoku;

/// <summary>
/// ЗАГЛУШКА для участника команды, отвечающего за судоку.
///
/// Что нужно сделать:
///   1. Реализовать рекурсивный перебор с возвратом (backtracking):
///      найти пустую клетку, перебрать кандидатов, поставить, уйти в рекурсию,
///      при неудаче снять значение и попробовать следующего кандидата.
///   2. На каждом шаге вызывать onStep (см. SudokuStepKind) — тогда UI сам
///      покажет анимацию поиска. Если onStep не нужен, можно игнорировать.
///   3. Считать recursiveCalls и Steps.
///   4. По желанию добавить эвристику MRV (клетка с наименьшим числом кандидатов).
///
/// Не изменяйте сигнатуру Solve — на неё рассчитан UI.
/// </summary>
public sealed class BacktrackingSudokuSolver : ISudokuSolver
{
    public string Name => "Рекурсивный перебор с возвратом";

    public SudokuSolveResult Solve(
        SudokuBoard board,
        Action<SudokuStep>? onStep = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(board);

        // TODO(судоку): реализовать алгоритм.
        // Подсказка: начните с копии доски: var work = board.Clone();
        // и рекурсивной функции SolveFrom(work, 0, depth).
        throw new NotImplementedException(
            "Решатель судоку ещё не реализован. " +
            "Откройте src/RecursiveLab.Core/Tasks/Sudoku/BacktrackingSudokuSolver.cs и заполните TODO.");
    }
}
