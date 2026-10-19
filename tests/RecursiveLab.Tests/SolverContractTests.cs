using RecursiveLab.Core.Tasks.Hanoi;
using RecursiveLab.Core.Tasks.Sudoku;

namespace RecursiveLab.Tests;

/// <summary>
/// Контрактные тесты для задач, которые реализуют участники команды.
/// Ханойские башни реализованы и проверяются; судоку пока заглушка,
/// поэтому его тест пропущен (Skip).
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

    [Fact]
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

    [Fact]
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

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(8)]
    public void HanoiSolvers_ProduceLegalMoves(int diskCount)
    {
        AssertLegalSolution(new RecursiveHanoiSolver(), diskCount);
        AssertLegalSolution(new StackHanoiSolver(), diskCount);
    }

    /// <summary>Проигрывает ходы и проверяет правила: верхнее кольцо, не большее на меньшее.</summary>
    private static void AssertLegalSolution(IHanoiSolver solver, int diskCount)
    {
        var rods = new List<int>[3];

        for (var rod = 0; rod < rods.Length; rod++)
        {
            rods[rod] = new List<int>();
        }

        // Снизу вверх: diskCount, diskCount-1, ..., 1 (верхнее кольцо — последний элемент).
        for (var disk = diskCount; disk >= 1; disk--)
        {
            rods[0].Add(disk);
        }

        var moves = solver.Solve(diskCount);
        Assert.Equal((1 << diskCount) - 1, moves.Count);

        foreach (var move in moves)
        {
            Assert.InRange(move.From, 0, 2);
            Assert.InRange(move.To, 0, 2);
            Assert.NotEqual(move.From, move.To);

            var source = rods[move.From];
            Assert.NotEmpty(source);
            Assert.Equal(move.Disk, source[^1]);

            var target = rods[move.To];
            if (target.Count > 0)
            {
                Assert.True(target[^1] > move.Disk, "нельзя класть большее кольцо на меньшее");
            }

            source.RemoveAt(source.Count - 1);
            target.Add(move.Disk);

            foreach (var rod in rods)
            {
                for (var index = 1; index < rod.Count; index++)
                {
                    Assert.True(rod[index - 1] > rod[index], "кольца должны уменьшаться снизу вверх");
                }
            }
        }

        Assert.Empty(rods[0]);
        Assert.Empty(rods[1]);
        Assert.Equal(Enumerable.Range(1, diskCount).Reverse(), rods[2]);
    }
}
