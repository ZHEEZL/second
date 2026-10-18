namespace RecursiveLab.Core.Tasks.Hanoi;

/// <summary>
/// ЗАГЛУШКА для участника команды, отвечающего за «Ханойские башни».
///
/// Задание 2 лабораторной: рекурсивное решение.
/// Доп. задание: то же самое через собственный стек (StackHanoiSolver).
///
/// Что нужно сделать:
///   1. Реализовать функцию Move(n, from, to):
///      — если n == 1, перенести диск 1;
///      — иначе перенести n-1 на вспомогательный стержень,
///        перенести диск n, вернуть n-1 на целевой.
///   2. Добавлять каждый ход в результат и вызывать onMove.
///   3. Количество ходов должно быть 2^n - 1.
///
/// Не изменяйте сигнатуру Solve — на неё рассчитан UI.
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

        // TODO(ханой): реализовать рекурсивное решение.
        throw new NotImplementedException(
            "Рекурсивное решение Ханойских башен ещё не реализовано. " +
            "Откройте src/RecursiveLab.Core/Tasks/Hanoi/RecursiveHanoiSolver.cs и заполните TODO.");
    }
}
