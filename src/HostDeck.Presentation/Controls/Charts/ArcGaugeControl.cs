using System;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace HostDeck.Presentation.Controls.Charts;

/// <summary>
/// Jauge arc dense style Netdata/maquettes. Sans valeur (<see cref="HasValue"/> faux) :
/// piste vide + libellé « — », aucune donnée inventée.
/// Géométries et <see cref="FormattedText"/> recalculés hors de <see cref="Render"/>.
/// </summary>
public sealed class ArcGaugeControl : Control
{
    private const double DefaultStartDegrees = 135;
    private const double DefaultSweepDegrees = 270;

    public static readonly StyledProperty<double?> ValueProperty =
        AvaloniaProperty.Register<ArcGaugeControl, double?>(nameof(Value));

    public static readonly StyledProperty<double> MaximumProperty =
        AvaloniaProperty.Register<ArcGaugeControl, double>(nameof(Maximum), 100d);

    public static readonly StyledProperty<IBrush?> StrokeProperty =
        AvaloniaProperty.Register<ArcGaugeControl, IBrush?>(nameof(Stroke));

    public static readonly StyledProperty<IBrush?> TrackBrushProperty =
        AvaloniaProperty.Register<ArcGaugeControl, IBrush?>(nameof(TrackBrush));

    public static readonly StyledProperty<IBrush?> LabelBrushProperty =
        AvaloniaProperty.Register<ArcGaugeControl, IBrush?>(nameof(LabelBrush));

    public static readonly StyledProperty<double> ThicknessProperty =
        AvaloniaProperty.Register<ArcGaugeControl, double>(nameof(Thickness), 8d);

    public static readonly StyledProperty<string?> EmptyLabelProperty =
        AvaloniaProperty.Register<ArcGaugeControl, string?>(nameof(EmptyLabel), "—");

    public static readonly StyledProperty<string?> UnitProperty =
        AvaloniaProperty.Register<ArcGaugeControl, string?>(nameof(Unit), "%");

    private StreamGeometry? _trackGeometry;
    private StreamGeometry? _valueGeometry;
    private Pen? _trackPen;
    private Pen? _valuePen;
    private IBrush? _cachedTrackBrush;
    private IBrush? _cachedStroke;
    private double _cachedThickness;
    private Size _cachedSize;
    private FormattedText? _labelText;
    private string? _cachedLabel;
    private IBrush? _cachedLabelBrush;
    private double _cachedFontSize;

    static ArcGaugeControl()
    {
        AffectsRender<ArcGaugeControl>(
            ValueProperty,
            MaximumProperty,
            StrokeProperty,
            TrackBrushProperty,
            LabelBrushProperty,
            ThicknessProperty,
            EmptyLabelProperty,
            UnitProperty);
        MinWidthProperty.OverrideDefaultValue<ArcGaugeControl>(72);
        MinHeightProperty.OverrideDefaultValue<ArcGaugeControl>(72);
    }

    /// <summary>Valeur 0..Maximum. Null = empty state (piste seule).</summary>
    public double? Value
    {
        get => GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public double Maximum
    {
        get => GetValue(MaximumProperty);
        set => SetValue(MaximumProperty, value);
    }

    public IBrush? Stroke
    {
        get => GetValue(StrokeProperty);
        set => SetValue(StrokeProperty, value);
    }

    public IBrush? TrackBrush
    {
        get => GetValue(TrackBrushProperty);
        set => SetValue(TrackBrushProperty, value);
    }

    public IBrush? LabelBrush
    {
        get => GetValue(LabelBrushProperty);
        set => SetValue(LabelBrushProperty, value);
    }

    public double Thickness
    {
        get => GetValue(ThicknessProperty);
        set => SetValue(ThicknessProperty, value);
    }

    public string? EmptyLabel
    {
        get => GetValue(EmptyLabelProperty);
        set => SetValue(EmptyLabelProperty, value);
    }

    public string? Unit
    {
        get => GetValue(UnitProperty);
        set => SetValue(UnitProperty, value);
    }

    public bool HasValue => Value is not null && Maximum > 0;

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ValueProperty
            || change.Property == MaximumProperty
            || change.Property == ThicknessProperty
            || change.Property == EmptyLabelProperty
            || change.Property == UnitProperty
            || change.Property == LabelBrushProperty
            || change.Property == StrokeProperty
            || change.Property == TrackBrushProperty)
        {
            Rebuild(Bounds.Size);
        }
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var arranged = base.ArrangeOverride(finalSize);
        if (arranged != _cachedSize)
        {
            Rebuild(arranged);
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

        EnsurePens();

        if (_trackGeometry is not null && _trackPen is not null)
        {
            context.DrawGeometry(null, _trackPen, _trackGeometry);
        }

        if (HasValue && _valueGeometry is not null && _valuePen is not null)
        {
            context.DrawGeometry(null, _valuePen, _valueGeometry);
        }

        if (_labelText is not null)
        {
            var origin = new Point(
                (width - _labelText.Width) / 2d,
                (height - _labelText.Height) / 2d);
            context.DrawText(_labelText, origin);
        }
    }

    private void Rebuild(Size size)
    {
        _cachedSize = size;
        _trackGeometry = null;
        _valueGeometry = null;
        _labelText = null;
        _cachedLabel = null;

        if (size.Width <= 0 || size.Height <= 0)
        {
            return;
        }

        var thickness = Math.Max(2d, Thickness);
        var radius = (Math.Min(size.Width, size.Height) / 2d) - (thickness / 2d) - 1d;
        if (radius <= 1d)
        {
            return;
        }

        var center = new Point(size.Width / 2d, size.Height / 2d);
        _trackGeometry = BuildArc(center, radius, DefaultStartDegrees, DefaultSweepDegrees);

        if (HasValue)
        {
            var ratio = Math.Clamp(Value!.Value / Maximum, 0d, 1d);
            var sweep = DefaultSweepDegrees * ratio;
            if (sweep > 0.5d)
            {
                _valueGeometry = BuildArc(center, radius, DefaultStartDegrees, sweep);
            }
        }

        RebuildLabel(size);
    }

    private void RebuildLabel(Size size)
    {
        var brush = LabelBrush;
        if (brush is null)
        {
            return;
        }

        string label;
        if (HasValue)
        {
            var unit = Unit ?? string.Empty;
            label = string.IsNullOrEmpty(unit)
                ? Value!.Value.ToString("0.#", CultureInfo.InvariantCulture)
                : Value!.Value.ToString("0.#", CultureInfo.InvariantCulture) + unit;
        }
        else
        {
            label = EmptyLabel ?? "—";
        }

        var fontSize = Math.Clamp(Math.Min(size.Width, size.Height) * 0.22d, 11d, 20d);
        if (_labelText is not null
            && string.Equals(_cachedLabel, label, StringComparison.Ordinal)
            && ReferenceEquals(_cachedLabelBrush, brush)
            && Math.Abs(_cachedFontSize - fontSize) < 0.01d)
        {
            return;
        }

        _cachedLabel = label;
        _cachedLabelBrush = brush;
        _cachedFontSize = fontSize;
        _labelText = new FormattedText(
            label,
            CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            new Typeface("Inter"),
            fontSize,
            brush);
    }

    private void EnsurePens()
    {
        var thickness = Math.Max(2d, Thickness);
        var track = TrackBrush;
        if (track is null)
        {
            _trackPen = null;
            _cachedTrackBrush = null;
        }
        else if (_trackPen is null
                 || !ReferenceEquals(_cachedTrackBrush, track)
                 || Math.Abs(_cachedThickness - thickness) > 0.01d)
        {
            _cachedTrackBrush = track;
            _trackPen = new Pen(track, thickness)
            {
                LineCap = PenLineCap.Round,
            };
        }

        var stroke = Stroke;
        if (stroke is null)
        {
            _valuePen = null;
            _cachedStroke = null;
        }
        else if (_valuePen is null
                 || !ReferenceEquals(_cachedStroke, stroke)
                 || Math.Abs(_cachedThickness - thickness) > 0.01d)
        {
            _cachedStroke = stroke;
            _valuePen = new Pen(stroke, thickness)
            {
                LineCap = PenLineCap.Round,
            };
        }

        _cachedThickness = thickness;
    }

    private static StreamGeometry BuildArc(Point center, double radius, double startDegrees, double sweepDegrees)
    {
        var start = PointOnCircle(center, radius, startDegrees);
        var end = PointOnCircle(center, radius, startDegrees + sweepDegrees);
        var isLarge = sweepDegrees > 180d;

        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            ctx.BeginFigure(start, isFilled: false);
            ctx.ArcTo(
                end,
                new Size(radius, radius),
                rotationAngle: 0,
                isLargeArc: isLarge,
                SweepDirection.Clockwise);
            ctx.EndFigure(false);
        }

        return geometry;
    }

    private static Point PointOnCircle(Point center, double radius, double degrees)
    {
        var radians = degrees * Math.PI / 180d;
        return new Point(
            center.X + (radius * Math.Cos(radians)),
            center.Y + (radius * Math.Sin(radians)));
    }
}
