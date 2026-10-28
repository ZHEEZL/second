using RecursiveLab.Core.Recursion;

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

    public IReadOnlyList<HanoiMove> Solve(int diskCount, Action<HanoiMove>? onMove = null) =>
        SolveTraced(diskCount, tracer: null, onMove);

    /// <summary>То же решение, но каждый вызов Move записывается в дерево рекурсии.</summary>
    public IReadOnlyList<HanoiMove> SolveTraced(
        int diskCount,
        IRecursionTracer? tracer,
        Action<HanoiMove>? onMove = null)
    {
        if (diskCount is < 1 or > 20)
        {
            throw new ArgumentOutOfRangeException(nameof(diskCount));
        }

        var moves = new List<HanoiMove>((1 << diskCount) - 1);
        Move(diskCount, from: 0, to: 2, aux: 1, moves, onMove, tracer);
        return moves;
    }

    private static void Move(
        int diskCount,
        int from,
        int to,
        int aux,
        List<HanoiMove> moves,
        Action<HanoiMove>? onMove,
        IRecursionTracer? tracer)
    {
        var node = tracer?.Enter(
            diskCount == 0 ? "0" : $"{diskCount}: {Peg(from)}→{Peg(to)}",
            $"Move(n = {diskCount}, {Peg(from)} → {Peg(to)}, через {Peg(aux)})") ?? 0;

        if (diskCount == 0)
        {
            tracer?.Exit(node, RecursionOutcome.BaseCase, "дисков нет — ничего не делаем");
            return;
        }

        Move(diskCount - 1, from, aux, to, moves, onMove, tracer);

        var move = new HanoiMove(diskCount, from, to);
        moves.Add(move);
        var moveNumber = moves.Count;
        onMove?.Invoke(move);

        Move(diskCount - 1, aux, to, from, moves, onMove, tracer);

        tracer?.Exit(
            node,
            RecursionOutcome.Completed,
            $"ход №{moveNumber}: диск {diskCount} {Peg(from)} → {Peg(to)}");
    }

    /// <summary>Имя стержня: 0 → A, 1 → B, 2 → C.</summary>
    public static string Peg(int index) => index switch
    {
        0 => "A",
        1 => "B",
        2 => "C",
        _ => index.ToString(System.Globalization.CultureInfo.InvariantCulture),
    };
}
