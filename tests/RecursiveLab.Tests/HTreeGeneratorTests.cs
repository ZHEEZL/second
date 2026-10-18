using RecursiveLab.Core.Geometry;
using RecursiveLab.Core.Tasks.HTree;

namespace RecursiveLab.Tests;

public class HTreeGeneratorTests
{
    [Theory]
    [InlineData(0, 3)]
    [InlineData(1, 15)]
    [InlineData(2, 63)]
    [InlineData(3, 255)]
    [InlineData(4, 1023)]
    [InlineData(5, 4095)]
    public void Generate_ProducesExpectedSegmentCount(int depth, int expected)
    {
        var segments = HTreeGenerator.Generate(1.0, depth);

        Assert.Equal(expected, segments.Count);
        Assert.Equal(expected, HTreeGenerator.SegmentCount(depth));
    }

    [Fact]
    public void Generate_DepthZero_IsSingleH()
    {
        var segments = HTreeGenerator.Generate(1.0, 0);

        Assert.Equal(3, segments.Count);
        Assert.All(segments, segment => Assert.Equal(0, segment.Depth));
        Assert.Equal(3, HTreeGenerator.SegmentCount(0));
        Assert.Equal(1, HTreeGenerator.HShapeCount(0));
    }

    [Fact]
    public void Generate_AssignsRecursionLevels()
    {
        const int depth = 3;
        var segments = HTreeGenerator.Generate(1.0, depth);

        var byLevel = segments.GroupBy(segment => segment.Depth).OrderBy(group => group.Key).ToList();

        Assert.Equal(depth + 1, byLevel.Count);

        for (var level = 0; level <= depth; level++)
        {
            var group = byLevel[level];
            var expectedCount = 3 * (int)Math.Pow(4, level);
            var expectedLength = Math.Pow(HTreeGenerator.ChildScale, level);

            Assert.Equal(level, group.Key);
            Assert.Equal(expectedCount, group.Count());

            foreach (var segment in group)
            {
                var length = Math.Abs(segment.End.X - segment.Start.X) + Math.Abs(segment.End.Y - segment.Start.Y);
                Assert.Equal(expectedLength, length, 6);
            }
        }
    }

    [Fact]
    public void Generate_DepthOne_HasExpectedBounds()
    {
        var segments = HTreeGenerator.Generate(1.0, 1);
        var (min, max) = HTreeGenerator.GetBounds(segments);

        var extent = 0.5 + 0.5 / Math.Sqrt(2);

        Assert.Equal(-extent, min.X, 6);
        Assert.Equal(-extent, min.Y, 6);
        Assert.Equal(extent, max.X, 6);
        Assert.Equal(extent, max.Y, 6);
    }

    [Fact]
    public void Generate_IsDeterministic()
    {
        var first = HTreeGenerator.Generate(2.0, 3);
        var second = HTreeGenerator.Generate(2.0, 3);

        Assert.Equal(first, second);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(HTreeGenerator.MaxDepth + 1)]
    public void Generate_RejectsInvalidDepth(int depth)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => HTreeGenerator.Generate(1.0, depth));
    }

    [Fact]
    public void Generate_RejectsInvalidSize()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => HTreeGenerator.Generate(0, 1));
    }

    [Fact]
    public void HShapeCount_MatchesSegmentFormula()
    {
        for (var depth = 0; depth <= 5; depth++)
        {
            var expected = (HTreeGenerator.SegmentCount(depth) + 1L) / 3;
            Assert.Equal(expected, HTreeGenerator.HShapeCount(depth));
        }
    }

    [Fact]
    public void GetBounds_OfEmptyList_IsZero()
    {
        var (min, max) = HTreeGenerator.GetBounds(Array.Empty<Segment>());

        Assert.Equal(new PointD(0, 0), min);
        Assert.Equal(new PointD(0, 0), max);
    }
}
