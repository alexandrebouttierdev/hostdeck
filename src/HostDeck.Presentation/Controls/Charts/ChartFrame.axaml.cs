using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace HostDeck.Presentation.Controls.Charts;

/// <summary>
/// Cadre de graphique : titre + tracé ou empty state explicite. Jamais de série inventée.
/// </summary>
public partial class ChartFrame : UserControl
{
    public static readonly StyledProperty<string> TitleProperty =
        AvaloniaProperty.Register<ChartFrame, string>(nameof(Title), string.Empty);

    public static readonly StyledProperty<string> EmptyTitleProperty =
        AvaloniaProperty.Register<ChartFrame, string>(nameof(EmptyTitle), "Aucune série disponible");

    public static readonly StyledProperty<string> EmptyDescriptionProperty =
        AvaloniaProperty.Register<ChartFrame, string>(
            nameof(EmptyDescription),
            "Les graphiques s’afficheront dès que des métriques seront collectées.");

    public static readonly StyledProperty<Geometry?> EmptyIconDataProperty =
        AvaloniaProperty.Register<ChartFrame, Geometry?>(nameof(EmptyIconData));

    public static readonly DirectProperty<ChartFrame, IReadOnlyList<ChartSeriesData>?> SeriesProperty =
        AvaloniaProperty.RegisterDirect<ChartFrame, IReadOnlyList<ChartSeriesData>?>(
            nameof(Series),
            o => o.Series,
            (o, v) => o.Series = v);

    public static readonly DirectProperty<ChartFrame, bool> HasSeriesProperty =
        AvaloniaProperty.RegisterDirect<ChartFrame, bool>(nameof(HasSeries), o => o.HasSeries);

    private IReadOnlyList<ChartSeriesData>? _series;
    private bool _hasSeries;

    public ChartFrame()
    {
        InitializeComponent();
    }

    public string Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public string EmptyTitle
    {
        get => GetValue(EmptyTitleProperty);
        set => SetValue(EmptyTitleProperty, value);
    }

    public string EmptyDescription
    {
        get => GetValue(EmptyDescriptionProperty);
        set => SetValue(EmptyDescriptionProperty, value);
    }

    public Geometry? EmptyIconData
    {
        get => GetValue(EmptyIconDataProperty);
        set => SetValue(EmptyIconDataProperty, value);
    }

    public IReadOnlyList<ChartSeriesData>? Series
    {
        get => _series;
        set
        {
            if (SetAndRaise(SeriesProperty, ref _series, value))
            {
                HasSeries = value is { Count: > 0 } list && list[0].Values.Count > 1;
            }
        }
    }

    public bool HasSeries
    {
        get => _hasSeries;
        private set => SetAndRaise(HasSeriesProperty, ref _hasSeries, value);
    }
}
