namespace RecursiveLab.Core.Tasks.Hanoi;

/// <summary>
/// ЗАГЛУШКА для доп. задания: то же решение, но без рекурсии —
/// с явным стеком вызовов (n, from, to, aux, state).
/// </summary>
public sealed class StackHanoiSolver : IHanoiSolver
{
    public string Name => "Через стек (без рекурсии)";

    public IReadOnlyList<HanoiMove> Solve(int diskCount, Action<HanoiMove>? onMove = null)
    {
        if (diskCount is < 1 or > 20)
        {
            throw new ArgumentOutOfRangeException(nameof(diskCount));
        }

        // TODO(ханой, доп.): реализовать итеративный вариант со стеком.
        throw new NotImplementedException(
            "Решение Ханойских башен через стек ещё не реализовано. " +
            "Откройте src/RecursiveLab.Core/Tasks/Hanoi/StackHanoiSolver.cs и заполните TODO.");
    }
}
