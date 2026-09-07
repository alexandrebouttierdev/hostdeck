using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace HostDeck.Presentation.Controls.Charts;

/// <summary>
/// Sparkline dense pour les cellules de table. Pas d'axes, pas de labels, pas de timer.
/// Géométrie recalculée hors de <see cref="Render"/> ; aucune allocation dans Render.
/// </summary>
public sealed class SparklineControl : Control
{
    public static readonly StyledProperty<IBrush?> StrokeProperty =
        AvaloniaProperty.Register<SparklineControl, IBrush?>(nameof(Stroke));

    public static readonly StyledProperty<IBrush?> FillProperty =
        AvaloniaProperty.Register<SparklineControl, IBrush?>(nameof(Fill));

    public static readonly StyledProperty<IBrush?> EmptyStrokeProperty =
        AvaloniaProperty.Register<SparklineControl, IBrush?>(nameof(EmptyStroke));

    public static readonly DirectProperty<SparklineControl, IReadOnlyList<float>?> ValuesProperty =
        AvaloniaProperty.RegisterDirect<SparklineControl, IReadOnlyList<float>?>(
            nameof(Values),
            o => o.Values,
            (o, v) => o.Values = v);

    private IReadOnlyList<float>? _values;
    private StreamGeometry? _lineGeometry;
    private StreamGeometry? _fillGeometry;
    private Pen? _strokePen;
    private Pen? _emptyPen;
    private IBrush? _cachedStroke;
    private IBrush? _cachedEmptyStroke;
    private Size _cachedSize;

    static SparklineControl()
    {
        AffectsRender<SparklineControl>(
            ValuesProperty,
            StrokeProperty,
            FillProperty,
            EmptyStrokeProperty);
        MinHeightProperty.OverrideDefaultValue<SparklineControl>(18);
        MinWidthProperty.OverrideDefaultValue<SparklineControl>(48);
    }

    public IReadOnlyList<float>? Values
    {
        get => _values;
        set
        {
            if (SetAndRaise(ValuesProperty, ref _values, value))
            {
                RebuildGeometries(Bounds.Size);
            }
        }
    }

    public IBrush? Stroke
    {
        get => GetValue(StrokeProperty);
        set => SetValue(StrokeProperty, value);
    }

    public IBrush? Fill
    {
        get => GetValue(FillProperty);
        set => SetValue(FillProperty, value);
    }

    public IBrush? EmptyStroke
    {
        get => GetValue(EmptyStrokeProperty);
        set => SetValue(EmptyStrokeProperty, value);
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

        if (_lineGeometry is null || Values is null || Values.Count < 2)
        {
            EnsureEmptyPen();
            if (_emptyPen is not null)
            {
                context.DrawRectangle(null, _emptyPen, new Rect(0.5, 0.5, width - 1, height - 1));
            }

            return;
        }

        if (_fillGeometry is not null && Fill is not null)
        {
            context.DrawGeometry(Fill, null, _fillGeometry);
        }

        EnsureStrokePen();
        if (_strokePen is not null)
        {
            context.DrawGeometry(null, _strokePen, _lineGeometry);
        }
    }

    private void RebuildGeometries(Size size)
    {
        _cachedSize = size;
        _lineGeometry = null;
        _fillGeometry = null;

        var values = Values;
        if (values is null || values.Count < 2 || size.Width <= 0 || size.Height <= 0)
        {
            return;
        }

        var min = float.MaxValue;
        var max = float.MinValue;
        for (var i = 0; i < values.Count; i++)
        {
            var v = values[i];
            if (v < min)
            {
                min = v;
            }

            if (v > max)
            {
                max = v;
            }
        }

        if (Math.Abs(max - min) < 0.0001f)
        {
            max = min + 1f;
        }

        var pad = 1.0;
        var usableWidth = Math.Max(1.0, size.Width - (pad * 2));
        var usableHeight = Math.Max(1.0, size.Height - (pad * 2));
        var stepX = usableWidth / (values.Count - 1);
        var range = max - min;

        var line = new StreamGeometry();
        using (var lineCtx = line.Open())
        {
            for (var i = 0; i < values.Count; i++)
            {
                var x = pad + (i * stepX);
                var y = pad + usableHeight - (((values[i] - min) / range) * usableHeight);
                var point = new Point(x, y);
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
            var first = new Point(pad, pad + usableHeight - (((values[0] - min) / range) * usableHeight));
            fillCtx.BeginFigure(first, isFilled: true);
            for (var i = 1; i < values.Count; i++)
            {
                var x = pad + (i * stepX);
                var y = pad + usableHeight - (((values[i] - min) / range) * usableHeight);
                fillCtx.LineTo(new Point(x, y));
            }

            fillCtx.LineTo(new Point(pad + usableWidth, pad + usableHeight));
            fillCtx.LineTo(new Point(pad, pad + usableHeight));
            fillCtx.EndFigure(true);
        }

        _lineGeometry = line;
        _fillGeometry = fill;
    }

    private void EnsureStrokePen()
    {
        var brush = Stroke;
        if (brush is null)
        {
            _strokePen = null;
            _cachedStroke = null;
            return;
        }

        if (_strokePen is not null && ReferenceEquals(_cachedStroke, brush))
        {
            return;
        }

        _cachedStroke = brush;
        _strokePen = new Pen(brush, 1.1);
    }

    private void EnsureEmptyPen()
    {
        var brush = EmptyStroke;
        if (brush is null)
        {
            _emptyPen = null;
            _cachedEmptyStroke = null;
            return;
        }

        if (_emptyPen is not null && ReferenceEquals(_cachedEmptyStroke, brush))
        {
            return;
        }

        _cachedEmptyStroke = brush;
        _emptyPen = new Pen(brush, 1.0);
    }
}
