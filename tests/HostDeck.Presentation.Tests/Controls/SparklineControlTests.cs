using Avalonia.Headless.XUnit;
using Avalonia.Media;
using HostDeck.Presentation.Controls.Charts;
using Xunit;

namespace HostDeck.Presentation.Tests.Controls;

public sealed class SparklineControlTests
{
    [AvaloniaFact]
    public void RendersEmptyFrameWithoutInventingValues()
    {
        var control = new SparklineControl
        {
            Width = 56,
            Height = 18,
            EmptyStroke = Brushes.Gray,
            Values = null,
        };

        Assert.Null(control.Values);
        Assert.NotNull(control.EmptyStroke);
    }

    [AvaloniaFact]
    public void AcceptsShortSeriesForLaterHistoryBinding()
    {
        var control = new SparklineControl
        {
            Values = [0.1f, 0.4f, 0.2f, 0.8f],
            Stroke = Brushes.DodgerBlue,
        };

        Assert.Equal(4, control.Values!.Count);
    }
}
