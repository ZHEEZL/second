using RecursiveLab.Core.Geometry;
using RecursiveLab.Core.Recursion;
using RecursiveLab.Core.Tasks.ClosestPair;
using RecursiveLab.Core.Tasks.Hanoi;
using RecursiveLab.Core.Tasks.HTree;
using RecursiveLab.Core.Tasks.Sudoku;

namespace RecursiveLab.Tests;

/// <summary>
/// Деревья рекурсии строятся из настоящих алгоритмов, поэтому их форма
/// должна совпадать с теорией: число вызовов, глубина, число базовых случаев.
/// </summary>
public class RecursionTreeTests
{
    private static RecursionTraceRecorder NewRecorder() => new("тест", "узел — вызов");

    // ---------------------------------------------------------------- записывающий

    [Fact]
    public void Recorder_BuildsNestedCallsLikeCallStack()
    {
        var recorder = NewRecorder();

        var root = recorder.Enter("root");
        var a = recorder.Enter("a");
        recorder.Exit(a, RecursionOutcome.BaseCase);
        var b = recorder.Enter("b");
        recorder.Note("пометка");
        recorder.Exit(b, RecursionOutcome.DeadEnd, "нет");
        recorder.Exit(root, RecursionOutcome.Completed);

        var tree = recorder.Build();

        Assert.Equal(3, tree.Nodes.Count);
        Assert.Equal(new[] { 0 }, tree.Roots);
        Assert.Equal(new[] { a, b }, tree.Nodes[root].Children);
        Assert.Equal(1, tree.Nodes[a].Depth);
        Assert.Equal(root, tree.Nodes[b].ParentId);
        Assert.Equal("пометка", tree.Nodes[b].Note);
        Assert.Equal("нет", tree.Nodes[b].Result);
        Assert.Equal(6, tree.Events.Count); // вход + выход на каждый вызов
        Assert.Equal(1, tree.MaxDepth);
        Assert.Equal(2, tree.LeafCount);
        Assert.False(tree.IsTruncated);
    }

    [Fact]
    public void Recorder_RejectsExitInWrongOrder()
    {
        var recorder = NewRecorder();
        var root = recorder.Enter("root");
        recorder.Enter("child");

        Assert.Throws<InvalidOperationException>(() => recorder.Exit(root, RecursionOutcome.Completed));
    }

    [Fact]
    public void Recorder_RejectsBuildWithOpenCalls()
    {
        var recorder = NewRecorder();
        recorder.Enter("root");

        Assert.Throws<InvalidOperationException>(() => recorder.Build());
    }

    [Fact]
    public void Recorder_DropsNodesOverLimitButCountsThem()
    {
        var recorder = new RecursionTraceRecorder("тест", "узел", maxNodes: 2);

        var root = recorder.Enter("root");
        var a = recorder.Enter("a");
        var b = recorder.Enter("b"); // сверх лимита
        recorder.Exit(b, RecursionOutcome.BaseCase);
        recorder.Exit(a, RecursionOutcome.Completed);
        recorder.Exit(root, RecursionOutcome.Completed);

        var tree = recorder.Build();

        Assert.Equal(RecursionTraceRecorder.DroppedNodeId, b);
        Assert.Equal(2, tree.Nodes.Count);
        Assert.Equal(3, tree.TotalCalls);
        Assert.Equal(1, tree.DroppedCalls);
        Assert.True(tree.IsTruncated);
    }

    // ---------------------------------------------------------------- H-дерево

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(5)]
    public void HTree_TreeMatchesTheory(int depth)
    {
        var recorder = NewRecorder();
        var traced = HTreeGenerator.Generate(1.0, depth, recorder);
        var tree = recorder.Build();

        // Вызовов столько же, сколько букв H; листьев (базовых случаев) — 4^depth.
        Assert.Equal(HTreeGenerator.HShapeCount(depth), tree.Nodes.Count);
        Assert.Equal((long)Math.Pow(4, depth), tree.Count(RecursionOutcome.BaseCase));
        Assert.Equal(depth, tree.MaxDepth);
        Assert.All(tree.Nodes.Where(n => n.Outcome == RecursionOutcome.Completed), n => Assert.Equal(4, n.Children.Count));

        // Запись не меняет результат алгоритма.
        Assert.Equal(HTreeGenerator.Generate(1.0, depth), traced);
    }

    // ---------------------------------------------------------------- Ханой

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(8)]
    public void Hanoi_TreeHasTwoToTheNPlusOneMinusOneCalls(int disks)
    {
        var recorder = NewRecorder();
        var moves = new RecursiveHanoiSolver().SolveTraced(disks, recorder);
        var tree = recorder.Build();

        Assert.Equal((1 << (disks + 1)) - 1, tree.Nodes.Count);       // все вызовы Move, включая n = 0
        Assert.Equal(1 << disks, tree.Count(RecursionOutcome.BaseCase)); // листья n = 0
        Assert.Equal((1 << disks) - 1, tree.Count(RecursionOutcome.Completed)); // по одному ходу на вызов
        Assert.Equal(disks, tree.MaxDepth);
        Assert.Equal(new RecursiveHanoiSolver().Solve(disks), moves);
    }

    // ---------------------------------------------------------------- судоку

    [Fact]
    public void SudokuSolver_TreeHasSolutionPathOfAllEmptyCells()
    {
        var board = SudokuBoard.Parse(SudokuPresets.All[0].Board);
        var empty = Enumerable.Range(0, SudokuBoard.CellCount).Count(i => board[i] == 0);

        var recorder = NewRecorder();
        var result = new BacktrackingSudokuSolver().SolveTraced(board, recorder);
        var tree = recorder.Build();

        Assert.True(result.Solved);
        Assert.Equal(
            new BacktrackingSudokuSolver().Solve(board).Board!.ToCompactString(),
            result.Board!.ToCompactString());

        if (!tree.IsTruncated)
        {
            // Корень + по узлу на каждую поставленную цифру.
            Assert.Equal(result.Steps + 1, tree.Nodes.Count);
            // Зелёный путь: корень и все клетки, заполненные в итоговом решении.
            Assert.Equal(empty + 1, tree.Count(RecursionOutcome.Success));
        }
    }

    [Theory]
    [InlineData("807109243491823675302407981915382467724516839638794152546271398289635714173948526", 2)]
    [InlineData("700804130430020008005310040076200010029048005040500009607002403000465082200000000", 1)]
    public void SolutionCounter_TracedCountEqualsPlainCount(string text, int expected)
    {
        var board = SudokuBoard.Parse(text);
        var recorder = NewRecorder();

        var count = SudokuSolutionCounter.CountSolutions(board, 2, out _, default, recorder);
        var tree = recorder.Build();

        Assert.Equal(expected, count);
        Assert.Equal(expected, SudokuSolutionCounter.CountSolutions(board, 2));
        Assert.Equal(RecursionOutcome.Success, tree.Nodes[tree.Roots[0]].Outcome);
    }

    [Fact]
    public void Generator_TracingDoesNotChangePuzzle()
    {
        var generator = new RandomizedSudokuGenerator();

        var plain = generator.Generate(new SudokuGenerationOptions(36, Seed: 11));
        var traced = generator.Generate(new SudokuGenerationOptions(36, Seed: 11, TraceRecursion: true));

        Assert.Null(plain.FillTree);
        Assert.NotNull(traced.FillTree);
        Assert.Equal(plain.Puzzle.ToCompactString(), traced.Puzzle.ToCompactString());

        var tree = traced.FillTree!;
        Assert.Equal(RecursionOutcome.Success, tree.Nodes[tree.Roots[0]].Outcome);
        Assert.Equal(SudokuBoard.CellCount + 1, tree.Count(RecursionOutcome.Success)); // корень + 81 клетка
    }

    // ---------------------------------------------------------------- ближайшая пара

    [Fact]
    public void ClosestPair_TreeMatchesRecursiveCalls()
    {
        var random = new Random(4);
        var points = Enumerable.Range(0, 200)
            .Select(_ => new PointD(random.NextDouble(), random.NextDouble()))
            .ToList();

        var finder = new DivideAndConquerClosestPair();
        var recorder = NewRecorder();
        var traced = finder.FindTraced(points, recorder);
        var tree = recorder.Build();

        Assert.Equal(traced.RecursiveCalls, tree.Nodes.Count);
        Assert.Equal(finder.Find(points).Distance, traced.Distance, 12);

        foreach (var node in tree.Nodes)
        {
            // База — лист; обычный вызов делит отрезок ровно на две половины.
            Assert.Equal(node.Outcome == RecursionOutcome.BaseCase ? 0 : 2, node.Children.Count);
        }
    }
}
