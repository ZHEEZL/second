using RecursiveLab.Core.Tasks.Hanoi;
using RecursiveLab.Core.Tasks.Sudoku;

namespace RecursiveLab.Tests;

/// <summary>
/// Контрактные тесты для задач, которые реализуют участники команды.
/// Пока алгоритмы — заглушки, тесты пропущены (Skip). После реализации
/// достаточно убрать Skip и запустить dotnet test.
/// </summary>
public class SolverContractTests
{
    [Fact(Skip = "Реализует участник 2: снять Skip после готовности BacktrackingSudokuSolver")]
    public void SudokuSolver_SolvesClassicPuzzle()
    {
        var board = SudokuBoard.Parse(SudokuPresets.All[0].Board);
        var solver = new BacktrackingSudokuSolver();

        var result = solver.Solve(board);

        Assert.True(result.Solved);
        Assert.NotNull(result.Board);
        Assert.True(result.Board!.IsSolved());
        Assert.False(board.IsSolved(), "Исходная доска не должна меняться.");
    }

    [Fact(Skip = "Реализует участник 4: снять Skip после готовности RecursiveHanoiSolver")]
    public void RecursiveHanoi_ReturnsTwoToThePowerOfNMoves()
    {
        var solver = new RecursiveHanoiSolver();

        for (var disks = 1; disks <= 8; disks++)
        {
            var moves = solver.Solve(disks);
            Assert.Equal((1 << disks) - 1, moves.Count);
            Assert.All(moves, move => Assert.InRange(move.From, 0, 2));
            Assert.All(moves, move => Assert.InRange(move.To, 0, 2));
            Assert.All(moves, move => Assert.NotEqual(move.From, move.To));
        }
    }

    [Fact(Skip = "Реализует участник 4 (доп.): снять Skip после готовности StackHanoiSolver")]
    public void StackHanoi_MatchesRecursiveHanoi()
    {
        var stackSolver = new StackHanoiSolver();
        var recursiveSolver = new RecursiveHanoiSolver();

        for (var disks = 1; disks <= 7; disks++)
        {
            var expected = recursiveSolver.Solve(disks);
            var actual = stackSolver.Solve(disks);

            Assert.Equal(expected.Count, actual.Count);
            Assert.Equal(expected, actual);
        }
    }
}
