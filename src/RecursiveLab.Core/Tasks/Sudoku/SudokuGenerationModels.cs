using RecursiveLab.Core.Recursion;

namespace RecursiveLab.Core.Tasks.Sudoku;

/// <summary>Уровень сложности генерируемой задачи.</summary>
public enum SudokuDifficulty
{
    Easy,
    Medium,
    Hard,
    Expert,
}

/// <summary>
/// Переводит уровень сложности в число подсказок.
/// Сложность = доля удалённых клеток: подсказок = 81 − round(81 · p).
/// </summary>
public static class SudokuDifficultyScale
{
    /// <summary>Меньше 17 подсказок у судоку с единственным решением не бывает (доказано в 2012 г.).</summary>
    public const int MinClues = 17;

    /// <summary>Хотя бы одну клетку нужно убрать, иначе это не задача.</summary>
    public const int MaxClues = 80;

    public static IReadOnlyList<SudokuDifficulty> All { get; } =
    [
        SudokuDifficulty.Easy,
        SudokuDifficulty.Medium,
        SudokuDifficulty.Hard,
        SudokuDifficulty.Expert,
    ];

    /// <summary>Доля клеток, которые нужно убрать (0..1).</summary>
    public static double RemovedShare(SudokuDifficulty difficulty) => difficulty switch
    {
        SudokuDifficulty.Easy => 0.45,
        SudokuDifficulty.Medium => 0.55,
        SudokuDifficulty.Hard => 0.62,
        SudokuDifficulty.Expert => 0.67,
        _ => throw new ArgumentOutOfRangeException(nameof(difficulty), difficulty, null),
    };

    /// <summary>Сколько подсказок должно остаться: 45 / 36 / 31 / 27.</summary>
    public static int ToTargetClues(SudokuDifficulty difficulty)
    {
        var removed = (int)Math.Round(
            SudokuBoard.CellCount * RemovedShare(difficulty),
            MidpointRounding.AwayFromZero);

        return SudokuBoard.CellCount - removed;
    }

    public static string DisplayName(SudokuDifficulty difficulty) => difficulty switch
    {
        SudokuDifficulty.Easy => "Лёгкая",
        SudokuDifficulty.Medium => "Средняя",
        SudokuDifficulty.Hard => "Сложная",
        SudokuDifficulty.Expert => "Эксперт",
        _ => difficulty.ToString(),
    };
}

/// <summary>
/// Параметры генерации.
/// <paramref name="TargetClues"/> — сколько подсказок оставить (17..80).
/// <paramref name="Seed"/> — зерно случайности; одинаковое зерно даёт одинаковую задачу.
/// <paramref name="MaxAttempts"/> — сколько раз пробовать заново, если цель не достигнута.
/// <paramref name="TraceRecursion"/> — записать дерево рекурсии построения сетки (Fill).
/// </summary>
public sealed record SudokuGenerationOptions(
    int TargetClues,
    int? Seed = null,
    int MaxAttempts = 20,
    bool TraceRecursion = false);

/// <summary>Результат генерации.</summary>
public sealed record SudokuPuzzleResult(
    SudokuBoard Puzzle,
    SudokuBoard Solution,
    int RequestedClues,
    int ActualClues,
    int SolutionCount,
    int Attempts,
    int Seed,
    long RecursiveCalls,
    TimeSpan Elapsed)
{
    /// <summary>Удалось ли оставить не больше подсказок, чем просили.</summary>
    public bool ReachedTarget => ActualClues <= RequestedClues;

    /// <summary>Доля удалённых клеток (0..1).</summary>
    public double RemovedShare => (SudokuBoard.CellCount - ActualClues) / (double)SudokuBoard.CellCount;

    /// <summary>Дерево рекурсии построения сетки (если просили TraceRecursion).</summary>
    public RecursionTree? FillTree { get; init; }
}

/// <summary>Контракт генератора судоку.</summary>
public interface ISudokuGenerator
{
    string Name { get; }

    /// <summary>
    /// Строит новую задачу с единственным решением.
    /// <paramref name="onAttempt"/> вызывается в начале каждой попытки (номер попытки, с 1).
    /// </summary>
    SudokuPuzzleResult Generate(
        SudokuGenerationOptions options,
        Action<int>? onAttempt = null,
        CancellationToken cancellationToken = default);
}
