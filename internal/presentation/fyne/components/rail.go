package components

import (
	"fmt"

	"fyne.io/fyne/v2"
	"fyne.io/fyne/v2/canvas"
	"fyne.io/fyne/v2/container"
	"fyne.io/fyne/v2/layout"
	"fyne.io/fyne/v2/widget"
	hdtheme "github.com/alexandrebouttierdev/hostdeck/internal/presentation/fyne/theme"
)

type ScreenID string

const (
	ScreenOverview       ScreenID = "overview"
	ScreenInfrastructure ScreenID = "infrastructure"
	ScreenHostDetails    ScreenID = "host_details"
	ScreenIncidents      ScreenID = "incidents"
	ScreenAlerts         ScreenID = "alerts"
	ScreenReports        ScreenID = "reports"
	ScreenTopology       ScreenID = "topology"
	ScreenLiveData       ScreenID = "live_data"
	ScreenDocker         ScreenID = "docker"
	ScreenSettings       ScreenID = "settings"
)

type Rail struct {
	widget.BaseWidget
	active   ScreenID
	onSelect func(ScreenID)
	badge    map[ScreenID]int
	buttons  map[ScreenID]*widget.Button
	root     *fyne.Container
}

func NewRail(active ScreenID, onSelect func(ScreenID)) *Rail {
	r := &Rail{active: active, onSelect: onSelect, badge: map[ScreenID]int{}, buttons: map[ScreenID]*widget.Button{}}
	r.ExtendBaseWidget(r)
	return r
}

func (r *Rail) SetActive(id ScreenID) {
	r.active = id
	for sid, b := range r.buttons {
		if sid == id {
			b.Importance = widget.HighImportance
		} else {
			b.Importance = widget.LowImportance
		}
		b.Refresh()
	}
}

func (r *Rail) SetBadge(id ScreenID, n int) {
	r.badge[id] = n
	if b, ok := r.buttons[id]; ok {
		sym := buttonSymbol(id)
		if n > 0 {
			b.SetText(fmt.Sprintf("%s %d", sym, n))
		} else {
			b.SetText(sym)
		}
	}
}

func buttonSymbol(id ScreenID) string {
	switch id {
	case ScreenOverview:
		return "▣"
	case ScreenInfrastructure:
		return "☰"
	case ScreenIncidents:
		return "⚠"
	case ScreenLiveData:
		return "⌁"
	case ScreenDocker:
		return "⬡"
	case ScreenAlerts:
		return "⚑"
	case ScreenReports:
		return "▤"
	case ScreenTopology:
		return "☍"
	case ScreenSettings:
		return "⚙"
	default:
		return "•"
	}
}

func (r *Rail) CreateRenderer() fyne.WidgetRenderer {
	bg := canvas.NewRectangle(hdtheme.ColorRail)
	logoBg := canvas.NewRectangle(hdtheme.ColorAccent)
	logoBg.CornerRadius = 6
	logoTxt := canvas.NewText("HD", hdtheme.ColorTextPrimary)
	logoTxt.TextStyle = fyne.TextStyle{Bold: true}
	logoTxt.Alignment = fyne.TextAlignCenter
	logo := container.NewStack(logoBg, container.NewPadded(logoTxt))

	order := []ScreenID{ScreenOverview, ScreenInfrastructure, ScreenIncidents, ScreenLiveData, ScreenDocker, ScreenAlerts, ScreenReports, ScreenTopology, ScreenSettings}
	box := container.NewVBox()
	for _, id := range order {
		sid := id
		b := widget.NewButton(buttonSymbol(sid), func() {
			if r.onSelect != nil {
				r.onSelect(sid)
			}
		})
		if sid == r.active {
			b.Importance = widget.HighImportance
		} else {
			b.Importance = widget.LowImportance
		}
		r.buttons[sid] = b
		box.Add(b)
	}
	r.root = container.NewStack(bg, container.NewBorder(container.NewPadded(logo), nil, nil, nil, container.NewVBox(box, layout.NewSpacer())))
	return widget.NewSimpleRenderer(r.root)
}

func (r *Rail) MinSize() fyne.Size { return fyne.NewSize(56, 240) }
