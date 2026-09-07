using Avalonia;
using HostDeck.Presentation.Views.Shell;
using Xunit;

namespace HostDeck.Presentation.Tests.Shell;

public sealed class WindowSizingTests
{
    [Fact]
    public void CalculatePreferredSizeKeepsDesignSizeWhenScreenCanContainIt()
    {
        var size = WindowSizing.CalculatePreferredSize(
            new PixelSize(2560, 1440),
            scaling: 1d,
            minimumSize: new Size(1100, 700));

        Assert.Equal(new Size(WindowSizing.DesignWidth, WindowSizing.DesignHeight), size);
    }

    [Fact]
    public void CalculatePreferredSizeConvertsPhysicalPixelsOnHiDpiScreen()
    {
        var size = WindowSizing.CalculatePreferredSize(
            new PixelSize(4608, 2472),
            scaling: 3d,
            minimumSize: new Size(1100, 700));

        Assert.Equal(1413.12d, size.Width, precision: 2);
        Assert.Equal(758.08d, size.Height, precision: 2);
    }

    [Theory]
    [InlineData(0d)]
    [InlineData(-1d)]
    [InlineData(double.NaN)]
    public void CalculatePreferredSizeFallsBackToOneForInvalidScaling(double scaling)
    {
        var size = WindowSizing.CalculatePreferredSize(
            new PixelSize(1920, 1080),
            scaling,
            minimumSize: new Size(1100, 700));

        Assert.Equal(1672d, size.Width);
        Assert.Equal(941d, size.Height);
    }
}
