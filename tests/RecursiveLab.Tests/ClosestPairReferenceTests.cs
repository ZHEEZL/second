using RecursiveLab.Core.Geometry;
using RecursiveLab.Core.Tasks.ClosestPair;

namespace RecursiveLab.Tests;

public class ClosestPairReferenceTests
{
    [Fact]
    public void FindsClosestPair_OnSmallSet()
    {
        var points = new[]
        {
            new PointD(0, 0),
            new PointD(3, 4),
            new PointD(1, 1),
        };

        var result = ClosestPairReference.FindBruteForce(points);

        Assert.Equal(Math.Sqrt(2), result.Distance, 9);
        Assert.Equal(3, result.DistanceComparisons);
    }

    [Fact]
    public void FindsZeroDistance_ForDuplicatePoints()
    {
        var points = new[]
        {
            new PointD(0, 0),
            new PointD(5, 5),
            new PointD(0, 0),
        };

        var result = ClosestPairReference.FindBruteForce(points);

        Assert.Equal(0, result.Distance, 12);
    }

    [Fact]
    public void Comparisons_MatchBruteForceFormula()
    {
        var random = new Random(42);
        var points = Enumerable.Range(0, 17)
            .Select(_ => new PointD(random.NextDouble(), random.NextDouble()))
            .ToArray();

        var result = ClosestPairReference.FindBruteForce(points);

        Assert.Equal(17 * 16 / 2, result.DistanceComparisons);
    }

    [Fact]
    public void Throws_ForLessThanTwoPoints()
    {
        Assert.Throws<ArgumentException>(() => ClosestPairReference.FindBruteForce(new[] { new PointD(1, 1) }));
    }

    // Когда участник 3 реализует DivideAndConquerClosestPair — уберите Skip.
    [Fact(Skip = "Реализует участник 3: снять Skip после готовности DivideAndConquerClosestPair")]
    public void DivideAndConquer_MatchesBruteForceOnRandomSets()
    {
        var random = new Random(12345);
        var finder = new DivideAndConquerClosestPair();

        for (var trial = 0; trial < 25; trial++)
        {
            var count = 2 + random.Next(400);
            var points = Enumerable.Range(0, count)
                .Select(_ => new PointD(random.NextDouble() * 100, random.NextDouble() * 100))
                .ToArray();

            var expected = ClosestPairReference.FindBruteForce(points);
            var actual = finder.Find(points);

            Assert.Equal(expected.Distance, actual.Distance, 9);
        }
    }
}
