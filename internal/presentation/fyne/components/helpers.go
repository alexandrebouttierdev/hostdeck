package components

import (
	"fmt"
	"image/color"

	"fyne.io/fyne/v2"
	"fyne.io/fyne/v2/canvas"
	"fyne.io/fyne/v2/container"
	"fyne.io/fyne/v2/widget"
	hdtheme "github.com/alexandrebouttierdev/hostdeck/internal/presentation/fyne/theme"
)

func Title(text string) *canvas.Text {
	t := canvas.NewText(text, hdtheme.ColorTextPrimary)
	t.TextSize = 18
	t.TextStyle = fyne.TextStyle{Bold: true}
	return t
}

func Muted(text string) *canvas.Text {
	t := canvas.NewText(text, hdtheme.ColorTextMuted)
	t.TextSize = 11
	return t
}

func FormatPct(v float64) string   { return fmt.Sprintf("%.0f%%", v) }
func FormatFloat(v float64) string { return fmt.Sprintf("%.1f", v) }

func Panel(title string, body fyne.CanvasObject) fyne.CanvasObject {
	bg := canvas.NewRectangle(hdtheme.ColorPanel)
	bg.CornerRadius = 6
	bg.StrokeColor = hdtheme.ColorBorder
	bg.StrokeWidth = 1
	h := canvas.NewText(title, hdtheme.ColorTextSecondary)
	h.TextSize = 11
	h.TextStyle = fyne.TextStyle{Bold: true}
	return container.NewStack(bg, container.NewBorder(container.NewPadded(h), nil, nil, nil, container.NewPadded(body)))
}

func KPI(title, value, sub string, accent color.Color) fyne.CanvasObject {
	bg := canvas.NewRectangle(hdtheme.ColorPanel)
	bg.CornerRadius = 6
	bg.StrokeColor = hdtheme.ColorBorder
	bg.StrokeWidth = 1
	t := canvas.NewText(title, hdtheme.ColorTextSecondary)
	t.TextSize = 11
	v := canvas.NewText(value, accent)
	v.TextSize = 22
	v.TextStyle = fyne.TextStyle{Bold: true}
	s := canvas.NewText(sub, hdtheme.ColorTextMuted)
	s.TextSize = 10
	return container.NewStack(bg, container.NewPadded(container.NewVBox(t, v, s)))
}

func StatusDot(status string) fyne.CanvasObject {
	dot := canvas.NewCircle(hdtheme.StatusColor(status))
	box := canvas.NewRectangle(color.Transparent)
	box.SetMinSize(fyne.NewSize(12, 12))
	return container.NewCenter(container.NewStack(box, dot))
}

func SeverityBadge(sev string) fyne.CanvasObject {
	labels := map[string]string{
		"critical": "Critique", "high": "Majeur", "warning": "Avertissement",
		"average": "Moyen", "information": "Info",
	}
	label := labels[sev]
	if label == "" {
		label = sev
	}
	bg := canvas.NewRectangle(hdtheme.SeverityColor(sev))
	bg.CornerRadius = 3
	txt := canvas.NewText(label, color.White)
	txt.TextSize = 10
	txt.TextStyle = fyne.TextStyle{Bold: true}
	return container.NewStack(bg, container.NewPadded(txt))
}

func GhostButton(label string, fn func()) *widget.Button {
	b := widget.NewButton(label, fn)
	b.Importance = widget.LowImportance
	return b
}

func PrimaryButton(label string, fn func()) *widget.Button {
	b := widget.NewButton(label, fn)
	b.Importance = widget.HighImportance
	return b
}
