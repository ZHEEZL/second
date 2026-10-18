namespace RecursiveLab.Core.Tasks.Sudoku;

/// <summary>Тип шага рекурсивного перебора с возвратом.</summary>
public enum SudokuStepKind
{
    /// <summary>Проверили кандидата (можно подсветить клетку, ничего не меняя).</summary>
    Try,

    /// <summary>Поставили значение в клетку.</summary>
    Place,

    /// <summary>Откатили значение (backtracking).</summary>
    Remove,

    /// <summary>Решение найдено.</summary>
    Solved,
}

/// <summary>
/// Шаг алгоритма для анимации. UI проигрывает шаги по порядку:
/// Place → значение в клетке, Remove → 0, Try/Solved → подсветка/статус.
/// </summary>
public readonly record struct SudokuStep(
    int CellIndex,
    int Value,
    SudokuStepKind Kind,
    int Depth);

public sealed record SudokuSolveResult(
    bool Solved,
    SudokuBoard? Board,
    int Steps,
    long RecursiveCalls,
    TimeSpan Elapsed);

/// <summary>
/// Контракт решателя судоку. Реализацию пишет участник команды,
/// отвечающий за судоку (файл BacktrackingSudokuSolver.cs).
/// </summary>
public interface ISudokuSolver
{
    string Name { get; }

    /// <summary>
    /// Решает копию доски. Исходная доска не должна изменяться.
    /// <paramref name="onStep"/> вызывается на каждом шаге (может быть null).
    /// </summary>
    SudokuSolveResult Solve(
        SudokuBoard board,
        Action<SudokuStep>? onStep = null,
        CancellationToken cancellationToken = default);
}
