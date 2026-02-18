using Microsoft.Xna.Framework;
using MonoBlackjack.Core.Ports;

namespace MonoBlackjack.App.Tests;

public class StatsDashboardTests
{
    [Fact]
    public void StatsFormatting_ComputeBankrollSummary_ReturnsLatestPeakAndTrough()
    {
        var points = new List<BankrollPoint>
        {
            new(1, -20m),
            new(2, 15m),
            new(3, 5m),
            new(4, 42m)
        };

        var summary = StatsFormatting.ComputeBankrollSummary(points);

        Assert.Equal(42m, summary.Latest);
        Assert.Equal(42m, summary.Peak);
        Assert.Equal(-20m, summary.Trough);
    }

    [Fact]
    public void StatsMatrixRenderer_ResolveMatrixCellColor_UsesNeutralColorWhenSampleIsLow()
    {
        var positive = StatsMatrixRenderer.ResolveMatrixCellColor(0.8f, sampleSize: 2);
        var negative = StatsMatrixRenderer.ResolveMatrixCellColor(-0.8f, sampleSize: 2);

        Assert.Equal(positive, negative);
    }

    [Fact]
    public void StatsMatrixRenderer_ResolveMatrixCellColor_TracksPerformanceDirectionAndConfidence()
    {
        var strongPositive = StatsMatrixRenderer.ResolveMatrixCellColor(0.45f, sampleSize: 20);
        var strongNegative = StatsMatrixRenderer.ResolveMatrixCellColor(-0.45f, sampleSize: 20);
        var lowConfidence = StatsMatrixRenderer.ResolveMatrixCellColor(0.45f, sampleSize: 5);

        Assert.True(strongPositive.G > strongPositive.R);
        Assert.True(strongNegative.R > strongNegative.G);
        Assert.True(strongPositive.A > lowConfidence.A);
    }

    [Fact]
    public void StatsMatrixRenderer_ResolveMatrixCellSize_ClampsForSmallAndLargeViewports()
    {
        float smallWidth = StatsMatrixRenderer.ResolveMatrixCellWidth(700f, 11);
        float largeWidth = StatsMatrixRenderer.ResolveMatrixCellWidth(3400f, 11);
        float smallHeight = StatsMatrixRenderer.ResolveMatrixCellHeight(600);
        float largeHeight = StatsMatrixRenderer.ResolveMatrixCellHeight(2160);

        Assert.InRange(smallWidth, 52f, 100f);
        Assert.Equal(100f, largeWidth);
        Assert.InRange(smallHeight, 24f, 46f);
        Assert.Equal(46f, largeHeight);
    }

    [Fact]
    public void StatsMatrixRenderer_ResolveMatrixTextColor_UsesContrast()
    {
        var dark = StatsMatrixRenderer.ResolveMatrixTextColor(new Color(20, 30, 40, 255));
        var bright = StatsMatrixRenderer.ResolveMatrixTextColor(new Color(210, 220, 230, 255));

        Assert.Equal(Color.White, dark);
        Assert.Equal(Color.Black, bright);
    }

    [Fact]
    public void StatsFormatting_FormatSignedCurrency_FormatsPositiveNegativeAndZero()
    {
        Assert.Equal("+$45", StatsFormatting.FormatSignedCurrency(45m));
        Assert.Equal("-$12", StatsFormatting.FormatSignedCurrency(-12m));
        Assert.Equal("$0", StatsFormatting.FormatSignedCurrency(0m));
    }
}
