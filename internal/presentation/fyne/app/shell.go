package app

import (
	"context"
	"fmt"
	"time"

	"fyne.io/fyne/v2"
	"fyne.io/fyne/v2/canvas"
	"fyne.io/fyne/v2/container"
	"fyne.io/fyne/v2/widget"
	"github.com/alexandrebouttierdev/hostdeck/internal/bootstrap"
	"github.com/alexandrebouttierdev/hostdeck/internal/presentation/fyne/components"
	"github.com/alexandrebouttierdev/hostdeck/internal/presentation/fyne/screens"
	hdtheme "github.com/alexandrebouttierdev/hostdeck/internal/presentation/fyne/theme"
)

type Shell struct {
	services *bootstrap.AppServices
	window   fyne.Window
	rail     *components.Rail
	content  *fyne.Container
	topTitle *canvas.Text
	status   *widget.Label
	active   components.ScreenID
	selected string
}

func NewShell(w fyne.Window, services *bootstrap.AppServices) *Shell {
	s := &Shell{services: services, window: w, active: components.ScreenOverview}
	s.rail = components.NewRail(s.active, s.navigate)
	s.topTitle = components.Title("Vue d'ensemble")
	s.status = widget.NewLabel("Collecte inactive")
	s.content = container.NewStack()
	s.navigate(components.ScreenOverview)
	return s
}

func (s *Shell) CanvasObject() fyne.CanvasObject {
	topBg := canvas.NewRectangle(hdtheme.ColorPanel)
	search := widget.NewEntry()
	search.SetPlaceHolder("Rechercher un hôte, un service, une métrique…")
	refresh := components.GhostButton("Actualiser", s.refresh)
	top := container.NewStack(topBg, container.NewBorder(nil, nil,
		container.NewPadded(s.topTitle),
		container.NewHBox(s.status, refresh),
		container.NewPadded(search),
	))
	bottomBg := canvas.NewRectangle(hdtheme.ColorRail)
	foot := widget.NewLabel("HostDeck — supervision locale multi-hôtes")
	foot.Importance = widget.LowImportance
	bottom := container.NewStack(bottomBg, container.NewPadded(foot))
	return container.NewBorder(top, bottom, s.rail, nil, s.content)
}

func (s *Shell) navigate(id components.ScreenID) {
	s.active = id
	s.rail.SetActive(id)
	var title string
	var body fyne.CanvasObject
	switch id {
	case components.ScreenOverview:
		title, body = "Vue d'ensemble", screens.Overview(s.services)
	case components.ScreenInfrastructure:
		title = "Infrastructure"
		body = screens.Infrastructure(s.window, s.services, func(id string) {
			s.selected = id
			s.navigate(components.ScreenHostDetails)
		})
	case components.ScreenHostDetails:
		title, body = "Détails de l'hôte", screens.HostDetails(s.services, s.selected)
	case components.ScreenIncidents:
		title, body = "Incidents actifs", screens.Incidents(s.window, s.services)
	case components.ScreenLiveData:
		title, body = "Données en direct", screens.LiveData(s.services, s.selected)
	case components.ScreenDocker:
		title, body = "Docker", screens.Docker(s.window, s.services, s.selected)
	case components.ScreenAlerts:
		title, body = "Règles d'alerte", screens.Alerts(s.window, s.services)
	case components.ScreenReports:
		title, body = "Rapports", screens.Reports(s.services)
	case components.ScreenTopology:
		title, body = "Topologie", screens.Topology(s.services)
	case components.ScreenSettings:
		title, body = "Paramètres", screens.Settings(s.services)
	default:
		title, body = "HostDeck", widget.NewLabel("Écran inconnu")
	}
	s.topTitle.Text = title
	s.topTitle.Refresh()
	s.content.Objects = []fyne.CanvasObject{container.NewPadded(body)}
	s.content.Refresh()
	s.refreshStatus()
}

func (s *Shell) refresh() { s.navigate(s.active) }

func (s *Shell) refreshStatus() {
	ctx, cancel := context.WithTimeout(context.Background(), 3*time.Second)
	defer cancel()
	ov, err := s.services.Overview.Build(ctx)
	if err != nil {
		s.status.SetText("Erreur de statut")
		return
	}
	state := "Collecte inactive"
	if ov.CollectionActive || s.services.Monitoring.IsFleetRunning() {
		state = "Collecte active"
	}
	s.status.SetText(fmt.Sprintf("%s · %d hôtes · %d incidents", state, ov.TotalHosts, ov.ActiveIncidents))
	s.rail.SetBadge(components.ScreenIncidents, ov.ActiveIncidents)
}
