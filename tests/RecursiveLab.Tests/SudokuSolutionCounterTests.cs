using RecursiveLab.Core.Tasks.Sudoku;

namespace RecursiveLab.Tests;

/// <summary>
/// Тесты рекурсивного подсчёта решений. Доски проверены отдельным
/// прототипом (тот же алгоритм на Python).
/// </summary>
public class SudokuSolutionCounterTests
{
    private const string SolvedGrid =
        "867159243491823675352467981915382467724516839638794152546271398289635714173948526";

    // Та же сетка без четырёх клеток прямоугольника (строки 1 и 3, столбцы 2 и 5):
    // цифры 6 и 5 в нём можно поменять местами — два решения.
    private const string TwoSolutions =
        "807109243491823675302407981915382467724516839638794152546271398289635714173948526";

    private const string UniqueWith36Clues =
        "700804130430020008005310040076200010029048005040500009607002403000465082200000000";

    // Строка 0: 1..8 и пусто, а девятка стоит в той же колонке ниже — клетке некуда деться.
    private const string DeadEnd =
        "123456780000000009000000000000000000000000000000000000000000000000000000000000000";

    private const string ConflictInGivens =
        "550000000000000000000000000000000000000000000000000000000000000000000000000000000";

    private const string Empty =
        "000000000000000000000000000000000000000000000000000000000000000000000000000000000";

    [Theory]
    [InlineData(SolvedGrid, 1)]
    [InlineData(TwoSolutions, 2)]
    [InlineData(UniqueWith36Clues, 1)]
    [InlineData(DeadEnd, 0)]
    [InlineData(ConflictInGivens, 0)]
    [InlineData(Empty, 2)]
    public void CountSolutions_WithLimitTwo_ReturnsExpected(string text, int expected)
    {
        var board = SudokuBoard.Parse(text);

        Assert.Equal(expected, SudokuSolutionCounter.CountSolutions(board, 2));
    }

    [Fact]
    public void CountSolutions_TwoSolutionPuzzle_CountsExactlyTwoWithHigherLimit()
    {
        var board = SudokuBoard.Parse(TwoSolutions);

        Assert.Equal(2, SudokuSolutionCounter.CountSolutions(board, 10));
    }

    [Fact]
    public void CountSolutions_LimitOne_StopsAtFirstSolution()
    {
        var board = SudokuBoard.Parse(TwoSolutions);

        Assert.Equal(1, SudokuSolutionCounter.CountSolutions(board, 1));
    }

    [Fact]
    public void CountSolutions_DoesNotChangeInputBoard()
    {
        var board = SudokuBoard.Parse(UniqueWith36Clues);
        var before = board.ToCompactString();

        SudokuSolutionCounter.CountSolutions(board, 2);

        Assert.Equal(before, board.ToCompactString());
    }

    [Fact]
    public void CountSolutions_ReportsRecursiveCalls()
    {
        var board = SudokuBoard.Parse(UniqueWith36Clues);

        var count = SudokuSolutionCounter.CountSolutions(board, 2, out var calls);

        Assert.Equal(1, count);
        Assert.True(calls > 0);
    }

    [Fact]
    public void HasUniqueSolution_DistinguishesOneFromTwo()
    {
        Assert.True(SudokuSolutionCounter.HasUniqueSolution(SudokuBoard.Parse(UniqueWith36Clues)));
        Assert.False(SudokuSolutionCounter.HasUniqueSolution(SudokuBoard.Parse(TwoSolutions)));
    }

    [Fact]
    public void CountSolutions_RejectsLimitBelowOne()
    {
        var board = SudokuBoard.Parse(Empty);

        Assert.Throws<ArgumentOutOfRangeException>(() => SudokuSolutionCounter.CountSolutions(board, 0));
    }

    [Fact]
    public void CountSolutions_RespectsCancellation()
    {
        var board = SudokuBoard.Parse(Empty);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        Assert.ThrowsAny<OperationCanceledException>(
            () => SudokuSolutionCounter.CountSolutions(board, 2, cts.Token));
    }
}
