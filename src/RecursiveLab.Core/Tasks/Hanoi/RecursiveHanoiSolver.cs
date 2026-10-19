namespace RecursiveLab.Core.Tasks.Hanoi;

/// <summary>
/// Рекурсивное решение «Ханойских башен» (задание 2 лабораторной).
///
/// Move(n, from, to, aux):
///   Move(n − 1, from, aux, to)   // убрать n−1 верхних колец на вспомогательный стержень
///   перенести диск n: from → to
///   Move(n − 1, aux, to, from)   // вернуть n−1 колец на целевой стержень
/// </summary>
public sealed class RecursiveHanoiSolver : IHanoiSolver
{
    public string Name => "Рекурсивное решение";

    public IReadOnlyList<HanoiMove> Solve(int diskCount, Action<HanoiMove>? onMove = null)
    {
        if (diskCount is < 1 or > 20)
        {
            throw new ArgumentOutOfRangeException(nameof(diskCount));
        }

        var moves = new List<HanoiMove>((1 << diskCount) - 1);
        Move(diskCount, from: 0, to: 2, aux: 1, moves, onMove);
        return moves;
    }

    private static void Move(
        int diskCount,
        int from,
        int to,
        int aux,
        List<HanoiMove> moves,
        Action<HanoiMove>? onMove)
    {
        if (diskCount == 0)
        {
            return;
        }

        Move(diskCount - 1, from, aux, to, moves, onMove);

        var move = new HanoiMove(diskCount, from, to);
        moves.Add(move);
        onMove?.Invoke(move);

        Move(diskCount - 1, aux, to, from, moves, onMove);
    }
}
