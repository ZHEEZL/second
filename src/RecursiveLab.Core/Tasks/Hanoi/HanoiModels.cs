namespace RecursiveLab.Core.Tasks.Hanoi;

/// <summary>Один ход: какое кольцо с какого стержня на какой перенести.</summary>
public readonly record struct HanoiMove(int Disk, int From, int To);

/// <summary>
/// Контракт решения головоломки «Ханойские башни».
/// Нужны две реализации: рекурсивная и через явный стек (доп. задание).
/// </summary>
public interface IHanoiSolver
{
    string Name { get; }

    /// <summary>
    /// Возвращает последовательность ходов для переноса башни со стержня 0
    /// на стержень 2. Диски нумеруются с 1 (1 — самый маленький).
    /// </summary>
    IReadOnlyList<HanoiMove> Solve(int diskCount, Action<HanoiMove>? onMove = null);
}
