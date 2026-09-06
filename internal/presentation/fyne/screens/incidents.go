package screens

import (
	"context"
	"fmt"
	"time"

	"fyne.io/fyne/v2"
	"fyne.io/fyne/v2/container"
	"fyne.io/fyne/v2/widget"
	"github.com/alexandrebouttierdev/hostdeck/internal/application/dto"
	"github.com/alexandrebouttierdev/hostdeck/internal/bootstrap"
	"github.com/alexandrebouttierdev/hostdeck/internal/presentation/fyne/components"
)

func Incidents(win fyne.Window, services *bootstrap.AppServices) fyne.CanvasObject {
	var root *fyne.Container
	var reload func()
	reload = func() {
		root.Objects = []fyne.CanvasObject{buildIncidents(win, services, reload)}
		root.Refresh()
	}
	root = container.NewStack(buildIncidents(win, services, reload))
	return root
}

func buildIncidents(win fyne.Window, services *bootstrap.AppServices, reload func()) fyne.CanvasObject {
	ctx, cancel := context.WithTimeout(context.Background(), 5*time.Second)
	defer cancel()
	list, err := services.Incidents.List(ctx, true)
	if err != nil {
		return widget.NewLabel(err.Error())
	}
	header := container.NewVBox(components.Title("Incidents actifs"), components.Muted(fmt.Sprintf("%d incidents", len(list))))
	selected := widget.NewLabel("Sélectionnez un incident")
	status := widget.NewLabel("")
	var current dto.IncidentDTO
	rows := container.NewVBox()
	for _, inc := range list {
		item := inc
		btn := widget.NewButton(fmt.Sprintf("%s · %s · %s", item.DisplayID, item.ServerName, item.Problem), func() {
			current = item
			selected.SetText(fmt.Sprintf("%s\nHôte : %s\nMétrique : %s\nProblème : %s\nSévérité : %s\nValeur : %.2f / seuil %.2f\nDurée : %d s\nStatut : %s",
				item.DisplayID, item.ServerName, item.Metric, item.Problem, item.Severity, item.CurrentValue, item.Threshold, item.DurationSecs, item.Status))
			status.SetText("")
		})
		btn.Importance = widget.LowImportance
		rows.Add(container.NewBorder(nil, nil, components.SeverityBadge(item.Severity), nil, btn))
	}
	if len(list) == 0 {
		rows.Add(components.Muted("Aucun incident actif"))
	}
	actions := container.NewHBox(
		components.PrimaryButton("Acquitter", func() {
			if current.ID == "" {
				status.SetText("Sélectionnez un incident")
				return
			}
			if _, err := services.Incidents.Acknowledge(context.Background(), dto.AcknowledgeIncidentDTO{IncidentID: current.ID, By: "opérateur", Notes: "Acquitté depuis l'UI"}); err != nil {
				status.SetText(err.Error())
				return
			}
			reload()
		}),
		components.GhostButton("Résoudre", func() {
			if current.ID == "" {
				status.SetText("Sélectionnez un incident")
				return
			}
			if _, err := services.Incidents.Resolve(context.Background(), dto.ResolveIncidentDTO{IncidentID: current.ID, Notes: "Résolu depuis l'UI"}); err != nil {
				status.SetText(err.Error())
				return
			}
			reload()
		}),
	)
	_ = win
	detail := components.Panel("Détail", container.NewVBox(selected, actions, status))
	return container.NewBorder(header, nil, nil, nil, container.NewHSplit(container.NewVScroll(rows), detail))
}
