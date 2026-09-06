package charts

import (
	"image/color"
	"math"

	"fyne.io/fyne/v2"
	"fyne.io/fyne/v2/canvas"
	"fyne.io/fyne/v2/widget"
	hdtheme "github.com/alexandrebouttierdev/hostdeck/internal/presentation/fyne/theme"
)

// Sparkline is a compact no-axis time series for dense tables.
type Sparkline struct {
	widget.BaseWidget
	Values []float64
	Stroke color.Color
	MinH   float32
}

func NewSparkline(values []float64, stroke color.Color) *Sparkline {
	if stroke == nil {
		stroke = hdtheme.ColorAccent
	}
	s := &Sparkline{Values: values, Stroke: stroke, MinH: 22}
	s.ExtendBaseWidget(s)
	return s
}

func (s *Sparkline) SetValues(values []float64) {
	s.Values = values
	s.Refresh()
}

func (s *Sparkline) CreateRenderer() fyne.WidgetRenderer {
	r := &sparkRenderer{spark: s}
	r.Refresh()
	return r
}

type sparkRenderer struct {
	spark *Sparkline
	objs  []fyne.CanvasObject
}

func (r *sparkRenderer) Layout(size fyne.Size)        { r.layoutLines(size) }
func (r *sparkRenderer) MinSize() fyne.Size           { return fyne.NewSize(72, r.spark.MinH) }
func (r *sparkRenderer) Objects() []fyne.CanvasObject { return r.objs }
func (r *sparkRenderer) Destroy()                     {}

func (r *sparkRenderer) Refresh() {
	r.layoutLines(r.spark.Size())
	canvas.Refresh(r.spark)
}

func (r *sparkRenderer) layoutLines(size fyne.Size) {
	vals := r.spark.Values
	if len(vals) < 2 || size.Width < 2 || size.Height < 2 {
		r.objs = nil
		return
	}
	minV, maxV := vals[0], vals[0]
	for _, v := range vals {
		if v < minV {
			minV = v
		}
		if v > maxV {
			maxV = v
		}
	}
	if math.Abs(maxV-minV) < 0.0001 {
		maxV = minV + 1
	}
	pad := float32(2)
	usableW := size.Width - pad*2
	usableH := size.Height - pad*2
	need := len(vals) - 1
	for len(r.objs) < need {
		line := canvas.NewLine(r.spark.Stroke)
		line.StrokeWidth = 1.25
		r.objs = append(r.objs, line)
	}
	r.objs = r.objs[:need]
	for i := 0; i < need; i++ {
		line := r.objs[i].(*canvas.Line)
		line.StrokeColor = r.spark.Stroke
		x1 := pad + usableW*float32(i)/float32(need)
		x2 := pad + usableW*float32(i+1)/float32(need)
		y1 := pad + usableH*(1-float32((vals[i]-minV)/(maxV-minV)))
		y2 := pad + usableH*(1-float32((vals[i+1]-minV)/(maxV-minV)))
		line.Position1 = fyne.NewPos(x1, y1)
		line.Position2 = fyne.NewPos(x2, y2)
	}
}
