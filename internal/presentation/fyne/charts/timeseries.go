package charts

import (
	"image/color"
	"math"

	"fyne.io/fyne/v2"
	"fyne.io/fyne/v2/canvas"
	"fyne.io/fyne/v2/container"
	"fyne.io/fyne/v2/widget"
	hdtheme "github.com/alexandrebouttierdev/hostdeck/internal/presentation/fyne/theme"
)

// Series is one named metric series.
type Series struct {
	Name   string
	Color  color.Color
	Points []float64
}

// TimeSeriesChart is a dense Netdata-like multi-series chart.
type TimeSeriesChart struct {
	widget.BaseWidget
	Title     string
	Series    []Series
	Threshold *float64
	YMax      float64
	MinHeight float32
}

func NewTimeSeriesChart(title string, series []Series) *TimeSeriesChart {
	c := &TimeSeriesChart{Title: title, Series: series, MinHeight: 140, YMax: 100}
	c.ExtendBaseWidget(c)
	return c
}

func (c *TimeSeriesChart) SetSeries(series []Series) {
	c.Series = series
	c.Refresh()
}

func (c *TimeSeriesChart) CreateRenderer() fyne.WidgetRenderer {
	bg := canvas.NewRectangle(hdtheme.ColorPanel)
	bg.CornerRadius = 4
	title := canvas.NewText(c.Title, hdtheme.ColorTextSecondary)
	title.TextSize = 11
	plot := newPlotArea(c)
	content := container.NewBorder(container.NewPadded(title), nil, nil, nil, container.NewPadded(plot))
	return widget.NewSimpleRenderer(container.NewStack(bg, content))
}

type plotArea struct {
	widget.BaseWidget
	chart *TimeSeriesChart
}

func newPlotArea(c *TimeSeriesChart) *plotArea {
	p := &plotArea{chart: c}
	p.ExtendBaseWidget(p)
	return p
}

func (p *plotArea) CreateRenderer() fyne.WidgetRenderer {
	r := &plotRenderer{plot: p}
	r.Refresh()
	return r
}

type plotRenderer struct {
	plot *plotArea
	objs []fyne.CanvasObject
}

func (r *plotRenderer) Layout(size fyne.Size) { r.rebuild(size) }
func (r *plotRenderer) MinSize() fyne.Size {
	h := r.plot.chart.MinHeight
	if h < 80 {
		h = 80
	}
	return fyne.NewSize(200, h)
}
func (r *plotRenderer) Objects() []fyne.CanvasObject { return r.objs }
func (r *plotRenderer) Destroy()                     {}
func (r *plotRenderer) Refresh() {
	r.rebuild(r.plot.Size())
	canvas.Refresh(r.plot)
}

func (r *plotRenderer) rebuild(size fyne.Size) {
	c := r.plot.chart
	if size.Width < 2 || size.Height < 2 {
		r.objs = nil
		return
	}
	objs := make([]fyne.CanvasObject, 0, 64)
	for i := 0; i <= 4; i++ {
		y := size.Height * float32(i) / 4
		line := canvas.NewLine(hdtheme.ColorBorder)
		line.StrokeWidth = 1
		line.Position1 = fyne.NewPos(0, y)
		line.Position2 = fyne.NewPos(size.Width, y)
		objs = append(objs, line)
	}
	ymax := c.YMax
	if ymax <= 0 {
		ymax = 100
	}
	for _, s := range c.Series {
		for _, v := range s.Points {
			if v > ymax {
				ymax = v
			}
		}
	}
	if c.Threshold != nil && *c.Threshold > ymax {
		ymax = *c.Threshold
	}
	for _, s := range c.Series {
		if len(s.Points) < 2 {
			continue
		}
		col := s.Color
		if col == nil {
			col = hdtheme.ColorAccent
		}
		n := len(s.Points) - 1
		for i := 0; i < n; i++ {
			line := canvas.NewLine(col)
			line.StrokeWidth = 1.4
			x1 := size.Width * float32(i) / float32(n)
			x2 := size.Width * float32(i+1) / float32(n)
			y1 := size.Height * (1 - float32(math.Max(0, s.Points[i])/ymax))
			y2 := size.Height * (1 - float32(math.Max(0, s.Points[i+1])/ymax))
			line.Position1 = fyne.NewPos(x1, y1)
			line.Position2 = fyne.NewPos(x2, y2)
			objs = append(objs, line)
		}
	}
	if c.Threshold != nil {
		y := size.Height * (1 - float32(*c.Threshold)/float32(ymax))
		line := canvas.NewLine(hdtheme.ColorThreshold)
		line.StrokeWidth = 1
		line.Position1 = fyne.NewPos(0, y)
		line.Position2 = fyne.NewPos(size.Width, y)
		objs = append(objs, line)
	}
	r.objs = objs
}
