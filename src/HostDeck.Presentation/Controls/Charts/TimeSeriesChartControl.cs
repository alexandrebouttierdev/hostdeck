using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace HostDeck.Presentation.Controls.Charts;

/// <summary>
/// Graphique technique style Netdata : grille fine, séries denses, invalidation via AffectsRender.
/// Aucune boucle 60 fps ; pas d'allocation dans <see cref="Render"/>.
/// </summary>
public sealed class TimeSeriesChartControl : Control
{
    private const int HorizontalGridLines = 4;
    private const int VerticalGridLines = 6;

    public static readonly StyledProperty<IBrush?> GridBrushProperty =
        AvaloniaProperty.Register<TimeSeriesChartControl, IBrush?>(nameof(GridBrush));

    public static readonly StyledProperty<IBrush?> PlotBackgroundProperty =
        AvaloniaProperty.Register<TimeSeriesChartControl, IBrush?>(nameof(PlotBackground));

    public static readonly DirectProperty<TimeSeriesChartControl, IReadOnlyList<ChartSeriesData>?> SeriesProperty =
        AvaloniaProperty.RegisterDirect<TimeSeriesChartControl, IReadOnlyList<ChartSeriesData>?>(
            nameof(Series),
            o => o.Series,
            (o, v) => o.Series = v);

    private IReadOnlyList<ChartSeriesData>? _series;
    private readonly List<CachedSeriesGeometry> _geometries = [];
    private Pen? _gridPen;
    private IBrush? _cachedGridBrush;
    private Size _cachedSize;

    static TimeSeriesChartControl()
    {
        AffectsRender<TimeSeriesChartControl>(SeriesProperty, GridBrushProperty, PlotBackgroundProperty);
        MinHeightProperty.OverrideDefaultValue<TimeSeriesChartControl>(120);
    }

    public IReadOnlyList<ChartSeriesData>? Series
    {
        get => _series;
        set
        {
            if (SetAndRaise(SeriesProperty, ref _series, value))
            {
                RebuildGeometries(Bounds.Size);
            }
        }
    }

    public IBrush? GridBrush
    {
        get => GetValue(GridBrushProperty);
        set => SetValue(GridBrushProperty, value);
    }

    public IBrush? PlotBackground
    {
        get => GetValue(PlotBackgroundProperty);
        set => SetValue(PlotBackgroundProperty, value);
    }

    public bool HasSeries => Series is { Count: > 0 } list && list[0].Values.Count > 1;

    protected override Size ArrangeOverride(Size finalSize)
    {
        var arranged = base.ArrangeOverride(finalSize);
        if (arranged != _cachedSize)
        {
            RebuildGeometries(arranged);
        }

        return arranged;
    }

    public override void Render(DrawingContext context)
    {
        var width = Bounds.Width;
        var height = Bounds.Height;
        if (width <= 0 || height <= 0)
        {
            return;
        }

        var plot = new Rect(0, 0, width, height);
        if (PlotBackground is not null)
        {
            context.FillRectangle(PlotBackground, plot);
        }

        DrawGrid(context, plot);

        for (var i = 0; i < _geometries.Count; i++)
        {
            var item = _geometries[i];
            if (item.FillGeometry is not null && item.FillBrush is not null)
            {
                context.DrawGeometry(item.FillBrush, null, item.FillGeometry);
            }

            if (item.LineGeometry is not null && item.LinePen is not null)
            {
                context.DrawGeometry(null, item.LinePen, item.LineGeometry);
            }
        }
    }

    private void DrawGrid(DrawingContext context, Rect plot)
    {
        EnsureGridPen();
        if (_gridPen is null)
        {
            return;
        }

        for (var i = 0; i <= HorizontalGridLines; i++)
        {
            var y = plot.Y + (plot.Height * i / HorizontalGridLines);
            context.DrawLine(_gridPen, new Point(plot.X, y), new Point(plot.Right, y));
        }

        for (var i = 0; i <= VerticalGridLines; i++)
        {
            var x = plot.X + (plot.Width * i / VerticalGridLines);
            context.DrawLine(_gridPen, new Point(x, plot.Y), new Point(x, plot.Bottom));
        }
    }

    private void RebuildGeometries(Size size)
    {
        _cachedSize = size;
        _geometries.Clear();

        var series = Series;
        if (series is null || series.Count == 0 || size.Width <= 0 || size.Height <= 0)
        {
            return;
        }

        var globalMin = float.MaxValue;
        var globalMax = float.MinValue;
        var pointCount = 0;
        for (var s = 0; s < series.Count; s++)
        {
            var values = series[s].Values;
            if (values.Count > pointCount)
            {
                pointCount = values.Count;
            }

            for (var i = 0; i < values.Count; i++)
            {
                var v = values[i];
                if (v < globalMin)
                {
                    globalMin = v;
                }

                if (v > globalMax)
                {
                    globalMax = v;
                }
            }
        }

        if (pointCount < 2)
        {
            return;
        }

        if (Math.Abs(globalMax - globalMin) < 0.0001f)
        {
            globalMax = globalMin + 1f;
        }

        var pad = 2.0;
        var usableWidth = Math.Max(1.0, size.Width - (pad * 2));
        var usableHeight = Math.Max(1.0, size.Height - (pad * 2));
        var range = globalMax - globalMin;

        for (var s = 0; s < series.Count; s++)
        {
            var entry = series[s];
            var values = entry.Values;
            if (values.Count < 2)
            {
                continue;
            }

            var stepX = usableWidth / (values.Count - 1);
            var line = new StreamGeometry();
            using (var lineCtx = line.Open())
            {
                for (var i = 0; i < values.Count; i++)
                {
                    var point = MapPoint(values[i], i, pad, stepX, usableHeight, globalMin, range);
                    if (i == 0)
                    {
                        lineCtx.BeginFigure(point, isFilled: false);
                    }
                    else
                    {
                        lineCtx.LineTo(point);
                    }
                }

                lineCtx.EndFigure(false);
            }

            var fill = new StreamGeometry();
            using (var fillCtx = fill.Open())
            {
                var first = MapPoint(values[0], 0, pad, stepX, usableHeight, globalMin, range);
                fillCtx.BeginFigure(first, isFilled: true);
                for (var i = 1; i < values.Count; i++)
                {
                    fillCtx.LineTo(MapPoint(values[i], i, pad, stepX, usableHeight, globalMin, range));
                }

                fillCtx.LineTo(new Point(pad + ((values.Count - 1) * stepX), pad + usableHeight));
                fillCtx.LineTo(new Point(pad, pad + usableHeight));
                fillCtx.EndFigure(true);
            }

            var lineBrush = new SolidColorBrush(entry.Color);
            var fillBrush = new SolidColorBrush(Color.FromArgb(64, entry.Color.R, entry.Color.G, entry.Color.B));
            _geometries.Add(new CachedSeriesGeometry(line, fill, new Pen(lineBrush, 1.2), fillBrush));
        }
    }

    private static Point MapPoint(
        float value,
        int index,
        double pad,
        double stepX,
        double usableHeight,
        float min,
        float range)
    {
        var x = pad + (index * stepX);
        var y = pad + usableHeight - (((value - min) / range) * usableHeight);
        return new Point(x, y);
    }

    private void EnsureGridPen()
    {
        var brush = GridBrush;
        if (brush is null)
        {
            _gridPen = null;
            _cachedGridBrush = null;
            return;
        }

        if (_gridPen is not null && ReferenceEquals(_cachedGridBrush, brush))
        {
            return;
        }

        _cachedGridBrush = brush;
        _gridPen = new Pen(brush, 1.0);
    }

    private sealed class CachedSeriesGeometry
    {
        public CachedSeriesGeometry(
            StreamGeometry lineGeometry,
            StreamGeometry fillGeometry,
            Pen linePen,
            IBrush fillBrush)
        {
            LineGeometry = lineGeometry;
            FillGeometry = fillGeometry;
            LinePen = linePen;
            FillBrush = fillBrush;
        }

        public StreamGeometry LineGeometry { get; }

        public StreamGeometry FillGeometry { get; }

        public Pen LinePen { get; }

        public IBrush FillBrush { get; }
    }
}
