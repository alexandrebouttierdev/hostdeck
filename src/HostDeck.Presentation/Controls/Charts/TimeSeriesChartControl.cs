using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace HostDeck.Presentation.Controls.Charts;

/// <summary>
/// Graphique technique style Netdata : grille fine, axes, aires empilées, légende compacte.
/// Invalidation via AffectsRender. Aucune allocation dans <see cref="Render"/>.
/// </summary>
public sealed class TimeSeriesChartControl : Control
{
    private const int HorizontalGridLines = 8;
    private const int VerticalGridLines = 12;
    private const double LeftAxisWidth = 34;
    private const double BottomAxisHeight = 16;
    private const double LegendHeight = 16;
    private const double PlotPadding = 2;
    private const double LabelFontSize = 10;
    private const int MinPixelsPerPoint = 2;

    public static readonly StyledProperty<IBrush?> GridBrushProperty =
        AvaloniaProperty.Register<TimeSeriesChartControl, IBrush?>(nameof(GridBrush));

    public static readonly StyledProperty<IBrush?> PlotBackgroundProperty =
        AvaloniaProperty.Register<TimeSeriesChartControl, IBrush?>(nameof(PlotBackground));

    public static readonly StyledProperty<IBrush?> AxisLabelBrushProperty =
        AvaloniaProperty.Register<TimeSeriesChartControl, IBrush?>(nameof(AxisLabelBrush));

    public static readonly StyledProperty<double?> YAxisMaxProperty =
        AvaloniaProperty.Register<TimeSeriesChartControl, double?>(nameof(YAxisMax));

    public static readonly StyledProperty<bool> StackedProperty =
        AvaloniaProperty.Register<TimeSeriesChartControl, bool>(nameof(Stacked));

    public static readonly DirectProperty<TimeSeriesChartControl, IReadOnlyList<ChartSeriesData>?> SeriesProperty =
        AvaloniaProperty.RegisterDirect<TimeSeriesChartControl, IReadOnlyList<ChartSeriesData>?>(
            nameof(Series),
            o => o.Series,
            (o, v) => o.Series = v);

    private IReadOnlyList<ChartSeriesData>? _series;
    private readonly List<CachedSeriesGeometry> _geometries = [];
    private readonly List<CachedLabel> _yLabels = [];
    private readonly List<CachedLabel> _xLabels = [];
    private readonly List<CachedLegendItem> _legendItems = [];
    private Pen? _gridPen;
    private Pen? _axisPen;
    private IBrush? _cachedGridBrush;
    private IBrush? _cachedAxisBrush;
    private Size _cachedSize;
    private Rect _plotRect;
    private bool _showLegend;
    private readonly Typeface _typeface = new(FontFamily.Default);

    static TimeSeriesChartControl()
    {
        AffectsRender<TimeSeriesChartControl>(
            SeriesProperty,
            GridBrushProperty,
            PlotBackgroundProperty,
            AxisLabelBrushProperty,
            YAxisMaxProperty,
            StackedProperty);
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

    public IBrush? AxisLabelBrush
    {
        get => GetValue(AxisLabelBrushProperty);
        set => SetValue(AxisLabelBrushProperty, value);
    }

    /// <summary>
    /// Plafond Y fixe (ex. 100 pour un %). Null = échelle auto selon le max (empilé ou mono).
    /// </summary>
    public double? YAxisMax
    {
        get => GetValue(YAxisMaxProperty);
        set => SetValue(YAxisMaxProperty, value);
    }

    /// <summary>
    /// Empile les séries (modes CPU). False = superposer (ex. réseau entrant/sortant).
    /// </summary>
    public bool Stacked
    {
        get => GetValue(StackedProperty);
        set => SetValue(StackedProperty, value);
    }

    public bool HasSeries => Series is { Count: > 0 } list && list[0].Values.Count > 1;

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == AxisLabelBrushProperty
            || change.Property == YAxisMaxProperty
            || change.Property == StackedProperty
            || change.Property == GridBrushProperty)
        {
            RebuildGeometries(Bounds.Size);
        }
    }

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

        if (PlotBackground is not null && _plotRect.Width > 0 && _plotRect.Height > 0)
        {
            context.FillRectangle(PlotBackground, _plotRect);
        }

        DrawGrid(context);

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

        EnsureAxisPen();
        if (_axisPen is not null && _plotRect.Width > 0)
        {
            context.DrawLine(
                _axisPen,
                new Point(_plotRect.X, _plotRect.Bottom),
                new Point(_plotRect.Right, _plotRect.Bottom));
            context.DrawLine(
                _axisPen,
                new Point(_plotRect.X, _plotRect.Y),
                new Point(_plotRect.X, _plotRect.Bottom));
        }

        for (var i = 0; i < _yLabels.Count; i++)
        {
            var label = _yLabels[i];
            context.DrawText(label.Text, label.Origin);
        }

        for (var i = 0; i < _xLabels.Count; i++)
        {
            var label = _xLabels[i];
            context.DrawText(label.Text, label.Origin);
        }

        for (var i = 0; i < _legendItems.Count; i++)
        {
            var item = _legendItems[i];
            context.FillRectangle(item.SwatchBrush, item.SwatchRect);
            context.DrawText(item.Text, item.TextOrigin);
        }
    }

    private void DrawGrid(DrawingContext context)
    {
        EnsureGridPen();
        if (_gridPen is null || _plotRect.Width <= 0 || _plotRect.Height <= 0)
        {
            return;
        }

        for (var i = 0; i <= HorizontalGridLines; i++)
        {
            var y = _plotRect.Y + (_plotRect.Height * i / HorizontalGridLines);
            context.DrawLine(_gridPen, new Point(_plotRect.X, y), new Point(_plotRect.Right, y));
        }

        for (var i = 0; i <= VerticalGridLines; i++)
        {
            var x = _plotRect.X + (_plotRect.Width * i / VerticalGridLines);
            context.DrawLine(_gridPen, new Point(x, _plotRect.Y), new Point(x, _plotRect.Bottom));
        }
    }

    private void RebuildGeometries(Size size)
    {
        _cachedSize = size;
        _geometries.Clear();
        _yLabels.Clear();
        _xLabels.Clear();
        _legendItems.Clear();
        _showLegend = false;
        _plotRect = default;

        var series = Series;
        if (series is null || series.Count == 0 || size.Width <= 0 || size.Height <= 0)
        {
            return;
        }

        _showLegend = series.Count > 1; // légende compacte dès qu'il y a plusieurs séries
        var bottomReserve = BottomAxisHeight + (_showLegend ? LegendHeight + 2 : 0);
        var plot = new Rect(
            LeftAxisWidth,
            PlotPadding,
            Math.Max(1.0, size.Width - LeftAxisWidth - PlotPadding),
            Math.Max(1.0, size.Height - bottomReserve - PlotPadding));
        _plotRect = plot;

        var prepared = PrepareSeries(series, plot.Width);
        if (prepared.Count == 0 || prepared[0].Values.Length < 2)
        {
            return;
        }

        var pointCount = prepared[0].Values.Length;
        var stacked = Stacked && prepared.Count > 1;
        var stacks = BuildStacks(prepared, pointCount, stacked);
        var dataMax = 0f;
        for (var i = 0; i < pointCount; i++)
        {
            var top = stacks[^1][i];
            if (top > dataMax)
            {
                dataMax = top;
            }
        }

        var yMax = ResolveYMax(dataMax);
        if (yMax < 0.0001)
        {
            yMax = 1;
        }

        BuildAxisLabels(plot, yMax, prepared[0].Timestamps, pointCount);
        if (_showLegend)
        {
            BuildLegend(plot, prepared, size.Height);
        }

        // Empilement : du haut vers le bas pour que les fills inférieurs restent visibles.
        for (var s = prepared.Count - 1; s >= 0; s--)
        {
            var entry = prepared[s];
            var top = stacks[s];
            var bottom = stacked && s > 0 ? stacks[s - 1] : null;
            var stepX = plot.Width / (pointCount - 1);

            var line = new StreamGeometry();
            using (var lineCtx = line.Open())
            {
                for (var i = 0; i < pointCount; i++)
                {
                    var point = MapPoint(top[i], i, plot, stepX, yMax);
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
                var first = MapPoint(top[0], 0, plot, stepX, yMax);
                fillCtx.BeginFigure(first, isFilled: true);
                for (var i = 1; i < pointCount; i++)
                {
                    fillCtx.LineTo(MapPoint(top[i], i, plot, stepX, yMax));
                }

                if (bottom is null)
                {
                    fillCtx.LineTo(new Point(plot.X + ((pointCount - 1) * stepX), plot.Bottom));
                    fillCtx.LineTo(new Point(plot.X, plot.Bottom));
                }
                else
                {
                    for (var i = pointCount - 1; i >= 0; i--)
                    {
                        fillCtx.LineTo(MapPoint(bottom[i], i, plot, stepX, yMax));
                    }
                }

                fillCtx.EndFigure(true);
            }

            var fillAlpha = stacked ? (byte)210 : prepared.Count > 1 ? (byte)90 : (byte)64;
            var lineBrush = new SolidColorBrush(entry.Color);
            var fillBrush = new SolidColorBrush(
                Color.FromArgb(fillAlpha, entry.Color.R, entry.Color.G, entry.Color.B));
            var thickness = stacked ? 1.0 : 1.35;
            _geometries.Add(new CachedSeriesGeometry(line, fill, new Pen(lineBrush, thickness), fillBrush));
        }
    }

    private static List<PreparedSeries> PrepareSeries(
        IReadOnlyList<ChartSeriesData> series,
        double plotWidth)
    {
        var maxPoints = Math.Max(2, (int)(plotWidth / MinPixelsPerPoint));
        var prepared = new List<PreparedSeries>(series.Count);
        var minCount = int.MaxValue;

        for (var s = 0; s < series.Count; s++)
        {
            var entry = series[s];
            if (entry.Values.Count < 2)
            {
                continue;
            }

            Downsample(entry.Values, entry.Timestamps, maxPoints, out var values, out var times);
            if (values.Length < 2)
            {
                continue;
            }

            prepared.Add(new PreparedSeries(entry.Name, values, entry.Color, times));
            if (values.Length < minCount)
            {
                minCount = values.Length;
            }
        }

        if (prepared.Count == 0)
        {
            return prepared;
        }

        // Aligner toutes les séries sur la plus courte pour un empilement cohérent.
        if (prepared.Count > 1)
        {
            for (var s = 0; s < prepared.Count; s++)
            {
                var item = prepared[s];
                if (item.Values.Length == minCount)
                {
                    continue;
                }

                var trimmed = new float[minCount];
                Array.Copy(item.Values, item.Values.Length - minCount, trimmed, 0, minCount);
                DateTimeOffset[]? times = null;
                if (item.Timestamps is not null && item.Timestamps.Length >= minCount)
                {
                    times = new DateTimeOffset[minCount];
                    Array.Copy(item.Timestamps, item.Timestamps.Length - minCount, times, 0, minCount);
                }

                prepared[s] = new PreparedSeries(item.Name, trimmed, item.Color, times);
            }
        }

        return prepared;
    }

    private static List<float[]> BuildStacks(List<PreparedSeries> prepared, int pointCount, bool stacked)
    {
        var stacks = new List<float[]>(prepared.Count);
        var running = new float[pointCount];
        for (var s = 0; s < prepared.Count; s++)
        {
            var values = prepared[s].Values;
            var layer = new float[pointCount];
            for (var i = 0; i < pointCount; i++)
            {
                var v = i < values.Length ? Math.Max(0f, values[i]) : 0f;
                if (stacked)
                {
                    running[i] += v;
                    layer[i] = running[i];
                }
                else
                {
                    layer[i] = v;
                }
            }

            stacks.Add(layer);
        }

        return stacks;
    }

    private double ResolveYMax(float dataMax)
    {
        if (YAxisMax is double fixedMax && fixedMax > 0)
        {
            return fixedMax;
        }

        if (dataMax <= 0)
        {
            return 1;
        }

        // Échelle « nice » : plafonds courants pour supervision.
        if (dataMax <= 1)
        {
            return 1;
        }

        var magnitude = Math.Pow(10, Math.Floor(Math.Log10(dataMax)));
        var normalized = dataMax / magnitude;
        double nice;
        if (normalized <= 1.5)
        {
            nice = 1.5;
        }
        else if (normalized <= 2)
        {
            nice = 2;
        }
        else if (normalized <= 5)
        {
            nice = 5;
        }
        else
        {
            nice = 10;
        }

        return nice * magnitude;
    }

    private void BuildAxisLabels(
        Rect plot,
        double yMax,
        DateTimeOffset[]? timestamps,
        int pointCount)
    {
        var brush = AxisLabelBrush;
        if (brush is null)
        {
            return;
        }

        // Y : 0 / 25 / 50 / 75 / 100 (ou ticks auto proportionnels).
        double[] fractions = [0, 0.25, 0.5, 0.75, 1.0];
        for (var i = 0; i < fractions.Length; i++)
        {
            var value = yMax * fractions[i];
            var text = FormatYLabel(value, yMax);
            var ft = CreateLabel(text, brush);
            var y = plot.Bottom - (plot.Height * fractions[i]) - (ft.Height / 2);
            y = Math.Clamp(y, plot.Y, plot.Bottom - ft.Height);
            var x = Math.Max(0, LeftAxisWidth - 4 - ft.Width);
            _yLabels.Add(new CachedLabel(ft, new Point(x, y)));
        }

        if (timestamps is null || timestamps.Length < 2 || pointCount < 2)
        {
            return;
        }

        var approxLabelWidth = 36.0;
        var maxLabels = Math.Max(2, (int)(plot.Width / approxLabelWidth));
        maxLabels = Math.Min(maxLabels, 8);
        var step = Math.Max(1, (pointCount - 1) / (maxLabels - 1));
        for (var i = 0; i < pointCount; i += step)
        {
            AddXLabel(plot, timestamps, pointCount, i, brush);
        }

        // Toujours borner le dernier horodatage à droite.
        var last = pointCount - 1;
        if ((last % step) != 0)
        {
            AddXLabel(plot, timestamps, pointCount, last, brush);
        }
    }

    private void AddXLabel(
        Rect plot,
        DateTimeOffset[] timestamps,
        int pointCount,
        int index,
        IBrush brush)
    {
        var tsIndex = Math.Min(index, timestamps.Length - 1);
        var local = timestamps[tsIndex].ToLocalTime();
        var text = local.ToString("HH:mm", CultureInfo.CurrentCulture);
        var ft = CreateLabel(text, brush);
        var ratio = (double)index / (pointCount - 1);
        var x = plot.X + (plot.Width * ratio) - (ft.Width / 2);
        x = Math.Clamp(x, plot.X, plot.Right - ft.Width);
        var y = plot.Bottom + 2;
        _xLabels.Add(new CachedLabel(ft, new Point(x, y)));
    }

    private void BuildLegend(Rect plot, List<PreparedSeries> prepared, double controlHeight)
    {
        var brush = AxisLabelBrush;
        if (brush is null)
        {
            return;
        }

        var x = plot.X;
        var y = controlHeight - LegendHeight;
        const double swatch = 8;
        const double gap = 10;
        for (var s = 0; s < prepared.Count; s++)
        {
            var entry = prepared[s];
            var ft = CreateLabel(entry.Name, brush);
            var needed = swatch + 4 + ft.Width + gap;
            if (x + needed > plot.Right && s > 0)
            {
                break;
            }

            var swatchBrush = new SolidColorBrush(entry.Color);
            _legendItems.Add(new CachedLegendItem(
                swatchBrush,
                new Rect(x, y + 3, swatch, swatch),
                ft,
                new Point(x + swatch + 4, y + ((LegendHeight - ft.Height) / 2))));
            x += needed;
        }
    }

    private static string FormatYLabel(double value, double yMax)
    {
        if (Math.Abs(yMax - 100) < 0.01)
        {
            return ((int)Math.Round(value)).ToString(CultureInfo.CurrentCulture);
        }

        if (yMax >= 1000)
        {
            return value.ToString("0.#", CultureInfo.CurrentCulture);
        }

        if (yMax <= 1)
        {
            return value.ToString("0.##", CultureInfo.CurrentCulture);
        }

        return value.ToString("0.#", CultureInfo.CurrentCulture);
    }

    private FormattedText CreateLabel(string text, IBrush brush) =>
        new(
            text,
            CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            _typeface,
            LabelFontSize,
            brush);

    private static Point MapPoint(float value, int index, Rect plot, double stepX, double yMax)
    {
        var x = plot.X + (index * stepX);
        var clamped = Math.Clamp(value, 0f, (float)yMax);
        var y = plot.Bottom - ((clamped / yMax) * plot.Height);
        return new Point(x, y);
    }

    private static void Downsample(
        IReadOnlyList<float> values,
        IReadOnlyList<DateTimeOffset>? timestamps,
        int maxPoints,
        out float[] outValues,
        out DateTimeOffset[]? outTimestamps)
    {
        var count = values.Count;
        if (count <= maxPoints)
        {
            outValues = new float[count];
            for (var i = 0; i < count; i++)
            {
                outValues[i] = values[i];
            }

            if (timestamps is not null && timestamps.Count == count)
            {
                outTimestamps = new DateTimeOffset[count];
                for (var i = 0; i < count; i++)
                {
                    outTimestamps[i] = timestamps[i];
                }
            }
            else
            {
                outTimestamps = null;
            }

            return;
        }

        // Bucket : conserve le max de chaque fenêtre (pics de supervision).
        outValues = new float[maxPoints];
        outTimestamps = timestamps is { Count: > 0 } && timestamps.Count == count
            ? new DateTimeOffset[maxPoints]
            : null;

        for (var i = 0; i < maxPoints; i++)
        {
            var start = (int)((long)i * count / maxPoints);
            var end = (int)((long)(i + 1) * count / maxPoints);
            if (end <= start)
            {
                end = Math.Min(count, start + 1);
            }

            var max = values[start];
            var maxIndex = start;
            for (var j = start + 1; j < end; j++)
            {
                if (values[j] > max)
                {
                    max = values[j];
                    maxIndex = j;
                }
            }

            outValues[i] = max;
            if (outTimestamps is not null)
            {
                outTimestamps[i] = timestamps![maxIndex];
            }
        }
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
        _gridPen = new Pen(brush, 0.5);
    }

    private void EnsureAxisPen()
    {
        var brush = AxisLabelBrush ?? GridBrush;
        if (brush is null)
        {
            _axisPen = null;
            _cachedAxisBrush = null;
            return;
        }

        if (_axisPen is not null && ReferenceEquals(_cachedAxisBrush, brush))
        {
            return;
        }

        _cachedAxisBrush = brush;
        _axisPen = new Pen(brush, 0.75);
    }

    private readonly struct PreparedSeries(
        string name,
        float[] values,
        Color color,
        DateTimeOffset[]? timestamps)
    {
        public string Name { get; } = name;
        public float[] Values { get; } = values;
        public Color Color { get; } = color;
        public DateTimeOffset[]? Timestamps { get; } = timestamps;
    }

    private sealed class CachedSeriesGeometry(
        StreamGeometry lineGeometry,
        StreamGeometry fillGeometry,
        Pen linePen,
        IBrush fillBrush)
    {
        public StreamGeometry LineGeometry { get; } = lineGeometry;
        public StreamGeometry FillGeometry { get; } = fillGeometry;
        public Pen LinePen { get; } = linePen;
        public IBrush FillBrush { get; } = fillBrush;
    }

    private sealed class CachedLabel(FormattedText text, Point origin)
    {
        public FormattedText Text { get; } = text;
        public Point Origin { get; } = origin;
    }

    private sealed class CachedLegendItem(
        IBrush swatchBrush,
        Rect swatchRect,
        FormattedText text,
        Point textOrigin)
    {
        public IBrush SwatchBrush { get; } = swatchBrush;
        public Rect SwatchRect { get; } = swatchRect;
        public FormattedText Text { get; } = text;
        public Point TextOrigin { get; } = textOrigin;
    }
}
