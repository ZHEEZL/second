using RecursiveLab.Core.Tasks.Sudoku;

namespace RecursiveLab.Tests;

public class SudokuBoardTests
{
    private const string ClassicPuzzle =
        "530070000" +
        "600195000" +
        "098000060" +
        "800060003" +
        "400803001" +
        "700020006" +
        "060000280" +
        "000419005" +
        "000080079";

    private const string ClassicSolution =
        "534678912" +
        "672195348" +
        "198342567" +
        "859761423" +
        "426853791" +
        "713924856" +
        "961537284" +
        "287419635" +
        "345286179";

    [Fact]
    public void Parse_ReadsGivensAndEmptyCells()
    {
        var board = SudokuBoard.Parse(ClassicPuzzle);

        Assert.Equal(5, board.Get(0, 0));
        Assert.Equal(3, board.Get(0, 1));
        Assert.Equal(0, board.Get(0, 2));
        Assert.True(board.IsGiven(0));
        Assert.False(board.IsGiven(2));
        Assert.Equal(81, board.ToCompactString().Length);
    }

    [Theory]
    [InlineData("123")]
    [InlineData("12345678912345678912345678912345678912345678912345678912345678912345678X")]
    public void Parse_RejectsBadInput(string text)
    {
        Assert.ThrowsAny<Exception>(() => SudokuBoard.Parse(text));
    }

    [Fact]
    public void IsValidPlacement_UsesRowColumnAndBoxRules()
    {
        var board = SudokuBoard.Parse(ClassicPuzzle);

        Assert.True(board.IsValidPlacement(2, 4));
        Assert.False(board.IsValidPlacement(2, 5));
        Assert.False(board.IsValidPlacement(2, 3));
    }

    [Fact]
    public void FindConflicts_ReportsBothCells()
    {
        var board = new SudokuBoard();
        board.SetGiven(0, 5);
        board.SetGiven(4, 5);

        var conflicts = board.FindConflicts();

        Assert.Contains(0, conflicts);
        Assert.Contains(4, conflicts);
    }

    [Fact]
    public void IsSolved_AcceptsCompleteValidBoard()
    {
        var board = SudokuBoard.Parse(ClassicSolution);

        Assert.True(board.IsSolved());
        Assert.Empty(board.FindConflicts());
    }

    [Fact]
    public void IsSolved_RejectsIncompleteBoard()
    {
        var board = SudokuBoard.Parse(ClassicPuzzle);

        Assert.False(board.IsSolved());
    }

    [Fact]
    public void TrySet_DoesNotChangeGivenCell()
    {
        var board = SudokuBoard.Parse(ClassicPuzzle);

        Assert.False(board.TrySet(0, 4));
        Assert.True(board.TrySet(2, 4));
        Assert.Equal(4, board[2]);
        Assert.True(board.TryClear(2));
        Assert.Equal(0, board[2]);
    }

    [Fact]
    public void Presets_AreParseableAndConsistent()
    {
        foreach (var preset in SudokuPresets.All)
        {
            var board = SudokuBoard.Parse(preset.Board);

            Assert.Equal(81, board.ToCompactString().Length);
            Assert.Empty(board.FindConflicts());
        }
    }
}
