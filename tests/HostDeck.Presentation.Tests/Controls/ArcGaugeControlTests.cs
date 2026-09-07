using Avalonia.Headless.XUnit;
using Avalonia.Media;
using HostDeck.Presentation.Controls.Charts;
using Xunit;

namespace HostDeck.Presentation.Tests.Controls;

public sealed class ArcGaugeControlTests
{
    [AvaloniaFact]
    public void EmptyGaugeHasNoInventedValue()
    {
        var control = new ArcGaugeControl
        {
            Width = 78,
            Height = 78,
            TrackBrush = Brushes.Gray,
            LabelBrush = Brushes.Gray,
            EmptyLabel = "—",
            Value = null,
        };

        Assert.False(control.HasValue);
        Assert.Null(control.Value);
        Assert.Equal("—", control.EmptyLabel);
    }

    [AvaloniaFact]
    public void AcceptsRealValueWithoutChangingMaximumDefault()
    {
        var control = new ArcGaugeControl
        {
            Value = 37.5,
            Stroke = Brushes.DodgerBlue,
            TrackBrush = Brushes.DimGray,
            LabelBrush = Brushes.White,
        };

        Assert.True(control.HasValue);
        Assert.Equal(37.5, control.Value);
        Assert.Equal(100d, control.Maximum);
    }
}
