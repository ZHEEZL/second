namespace RecursiveLab.Core.Tasks.Hanoi;

/// <summary>
/// Доп. задание: то же решение без рекурсии — через явный стек кадров.
///
/// Кадр хранит подзадачу (n, from, to, aux) и стадию, на которой она находится.
/// Стадии повторяют три шага рекурсивного алгоритма:
///   1) перенести n−1 кольцо на вспомогательный стержень;
///   2) перенести диск n на целевой;
///   3) вернуть n−1 кольцо на целевой.
/// </summary>
public sealed class StackHanoiSolver : IHanoiSolver
{
    private enum Stage
    {
        MoveLeftSubtree,
        MoveLargestDisk,
        MoveRightSubtree,
    }

    private readonly record struct Frame(
        int DiskCount,
        int From,
        int To,
        int Aux,
        Stage Stage);

    public string Name => "Через стек (без рекурсии)";

    public IReadOnlyList<HanoiMove> Solve(int diskCount, Action<HanoiMove>? onMove = null)
    {
        if (diskCount is < 1 or > 20)
        {
            throw new ArgumentOutOfRangeException(nameof(diskCount));
        }

        var moves = new List<HanoiMove>((1 << diskCount) - 1);
        var stack = new Stack<Frame>();
        stack.Push(new Frame(diskCount, From: 0, To: 2, Aux: 1, Stage.MoveLeftSubtree));

        while (stack.Count > 0)
        {
            var frame = stack.Pop();

            if (frame.DiskCount == 0)
            {
                continue;
            }

            switch (frame.Stage)
            {
                case Stage.MoveLeftSubtree:
                    stack.Push(frame with { Stage = Stage.MoveLargestDisk });
                    stack.Push(new Frame(
                        frame.DiskCount - 1,
                        frame.From,
                        frame.Aux,
                        frame.To,
                        Stage.MoveLeftSubtree));
                    break;

                case Stage.MoveLargestDisk:
                    stack.Push(frame with { Stage = Stage.MoveRightSubtree });

                    var move = new HanoiMove(frame.DiskCount, frame.From, frame.To);
                    moves.Add(move);
                    onMove?.Invoke(move);
                    break;

                case Stage.MoveRightSubtree:
                    stack.Push(new Frame(
                        frame.DiskCount - 1,
                        frame.Aux,
                        frame.To,
                        frame.From,
                        Stage.MoveLeftSubtree));
                    break;
            }
        }

        return moves;
    }
}
