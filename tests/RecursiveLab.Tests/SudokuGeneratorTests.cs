using RecursiveLab.Core.Tasks.Sudoku;

namespace RecursiveLab.Tests;

public class SudokuGeneratorTests
{
    private readonly RandomizedSudokuGenerator _generator = new();

    [Theory]
    [InlineData(1)]
    [InlineData(7)]
    [InlineData(2026)]
    public void CreateSolvedGrid_IsCompleteAndValid(int seed)
    {
        var grid = RandomizedSudokuGenerator.CreateSolvedGrid(new Random(seed));

        Assert.True(grid.IsSolved());
    }

    [Fact]
    public void CreateSolvedGrid_SameSeedSameGrid_DifferentSeedDifferentGrid()
    {
        var a = RandomizedSudokuGenerator.CreateSolvedGrid(new Random(42)).ToCompactString();
        var b = RandomizedSudokuGenerator.CreateSolvedGrid(new Random(42)).ToCompactString();
        var c = RandomizedSudokuGenerator.CreateSolvedGrid(new Random(43)).ToCompactString();

        Assert.Equal(a, b);
        Assert.NotEqual(a, c);
    }

    [Fact]
    public void Shuffle_KeepsAllElements()
    {
        var items = Enumerable.Range(1, 9).ToList();

        RandomizedSudokuGenerator.Shuffle(items, new Random(5));

        Assert.Equal(Enumerable.Range(1, 9), items.OrderBy(x => x));
    }

    [Fact]
    public void BoxRoundRobinOrder_IsPermutationAndEveryRoundCoversAllBoxes()
    {
        var order = RandomizedSudokuGenerator.BoxRoundRobinOrder(new Random(3));

        Assert.Equal(Enumerable.Range(0, SudokuBoard.CellCount), order.OrderBy(x => x));

        for (var round = 0; round < SudokuBoard.Size; round++)
        {
            var boxes = order
                .Skip(round * SudokuBoard.Size)
                .Take(SudokuBoard.Size)
                .Select(RandomizedSudokuGenerator.BoxOf)
                .OrderBy(x => x);

            Assert.Equal(Enumerable.Range(0, SudokuBoard.Size), boxes);
        }
    }

    [Theory]
    [InlineData(SudokuDifficulty.Easy, 45)]
    [InlineData(SudokuDifficulty.Medium, 36)]
    [InlineData(SudokuDifficulty.Hard, 31)]
    [InlineData(SudokuDifficulty.Expert, 27)]
    public void DifficultyScale_MapsToClues(SudokuDifficulty difficulty, int expectedClues)
    {
        Assert.Equal(expectedClues, SudokuDifficultyScale.ToTargetClues(difficulty));
    }

    [Theory]
    [InlineData(45, 1)]
    [InlineData(36, 2)]
    [InlineData(30, 3)]
    public void Generate_ProducesValidUniquePuzzle(int targetClues, int seed)
    {
        var result = _generator.Generate(new SudokuGenerationOptions(targetClues, seed));

        // Цель достижима всегда для этих чисел (см. замеры в теории).
        Assert.True(result.ReachedTarget);
        Assert.Equal(targetClues, result.ActualClues);
        Assert.Equal(1, result.SolutionCount);
        Assert.Equal(seed, result.Seed);

        Assert.True(result.Solution.IsSolved());
        Assert.Empty(result.Puzzle.FindConflicts());
        Assert.Equal(1, SudokuSolutionCounter.CountSolutions(result.Puzzle, 2));

        var clues = 0;
        for (var index = 0; index < SudokuBoard.CellCount; index++)
        {
            var value = result.Puzzle[index];
            if (value == 0)
            {
                Assert.False(result.Puzzle.IsGiven(index));
                continue;
            }

            clues++;
            Assert.True(result.Puzzle.IsGiven(index), "Оставшиеся клетки должны быть стартовыми.");
            Assert.Equal(result.Solution[index], value); // Задача — подмножество решения.
        }

        Assert.Equal(result.ActualClues, clues);
    }

    [Fact]
    public void Generate_SameSeedGivesSamePuzzle()
    {
        var options = new SudokuGenerationOptions(36, Seed: 100);

        var first = _generator.Generate(options);
        var second = _generator.Generate(options);

        Assert.Equal(first.Puzzle.ToCompactString(), second.Puzzle.ToCompactString());
        Assert.Equal(first.Solution.ToCompactString(), second.Solution.ToCompactString());
    }

    [Fact]
    public void Generate_UnreachableTarget_ReturnsHonestUniqueResult()
    {
        var result = _generator.Generate(new SudokuGenerationOptions(17, Seed: 1, MaxAttempts: 2));

        Assert.InRange(result.ActualClues, SudokuDifficultyScale.MinClues, SudokuDifficultyScale.MaxClues);
        Assert.Equal(1, result.SolutionCount);

        if (!result.ReachedTarget)
        {
            Assert.Equal(2, result.Attempts); // Пробовал все разрешённые попытки.
        }
    }

    [Fact]
    public void Generate_RemovesCellsEvenlyAcrossBoxes()
    {
        for (var seed = 1; seed <= 10; seed++)
        {
            var result = _generator.Generate(new SudokuGenerationOptions(36, seed));

            var perBox = new int[SudokuBoard.Size];
            for (var index = 0; index < SudokuBoard.CellCount; index++)
            {
                if (result.Puzzle[index] != 0)
                {
                    perBox[RandomizedSudokuGenerator.BoxOf(index)]++;
                }
            }

            // На прототипе разброс был не больше 3; берём порог с запасом.
            Assert.True(perBox.Max() - perBox.Min() <= 5, $"seed {seed}: {string.Join(",", perBox)}");
        }
    }

    [Fact]
    public void Generate_ReportsAttempts()
    {
        var attempts = new List<int>();

        _generator.Generate(new SudokuGenerationOptions(45, Seed: 9), attempts.Add);

        Assert.Equal(1, attempts[0]);
    }

    [Theory]
    [InlineData(16)]
    [InlineData(81)]
    public void Generate_RejectsBadTarget(int targetClues)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => _generator.Generate(new SudokuGenerationOptions(targetClues, Seed: 1)));
    }

    [Fact]
    public void Generate_RejectsZeroAttempts()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => _generator.Generate(new SudokuGenerationOptions(36, Seed: 1, MaxAttempts: 0)));
    }

    [Fact]
    public void Generate_RespectsCancellation()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        Assert.ThrowsAny<OperationCanceledException>(
            () => _generator.Generate(new SudokuGenerationOptions(36, Seed: 1), cancellationToken: cts.Token));
    }
}
