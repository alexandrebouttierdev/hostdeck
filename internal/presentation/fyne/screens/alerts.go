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
	"github.com/alexandrebouttierdev/hostdeck/internal/presentation/fyne/dialogs"
)

// Alerts lists alert rules with create/edit/delete.
func Alerts(win fyne.Window, services *bootstrap.AppServices) fyne.CanvasObject {
	var root *fyne.Container
	var reload func()
	reload = func() {
		root.Objects = []fyne.CanvasObject{buildAlerts(win, services, reload)}
		root.Refresh()
	}
	root = container.NewStack(buildAlerts(win, services, reload))
	return root
}

func buildAlerts(win fyne.Window, services *bootstrap.AppServices, reload func()) fyne.CanvasObject {
	ctx, cancel := context.WithTimeout(context.Background(), 5*time.Second)
	defer cancel()
	rules, err := services.Alerts.List(ctx)
	if err != nil {
		return widget.NewLabel(err.Error())
	}

	var selected *dto.AlertRuleDTO
	status := widget.NewLabel("")

	addBtn := components.PrimaryButton("Ajouter", func() {
		dialogs.ShowAlertRuleDialog(win, services, nil, reload)
	})
	editBtn := components.GhostButton("Modifier", func() {
		if selected == nil {
			status.SetText("Sélectionnez une règle")
			return
		}
		cp := *selected
		dialogs.ShowAlertRuleDialog(win, services, &cp, reload)
	})
	delBtn := components.GhostButton("Supprimer", func() {
		if selected == nil {
			status.SetText("Sélectionnez une règle")
			return
		}
		dialogs.ConfirmDeleteAlert(win, services, selected.ID, selected.Name, reload)
	})
	toggleBtn := components.GhostButton("Activer/Désactiver", func() {
		if selected == nil {
			status.SetText("Sélectionnez une règle")
			return
		}
		cp := *selected
		cp.Enabled = !cp.Enabled
		ctx, cancel := context.WithTimeout(context.Background(), 5*time.Second)
		defer cancel()
		if _, err := services.Alerts.Update(ctx, cp); err != nil {
			status.SetText(err.Error())
			return
		}
		reload()
	})

	header := container.NewBorder(nil, nil, nil,
		container.NewHBox(addBtn, editBtn, delBtn, toggleBtn),
		container.NewVBox(
			components.Title("Règles d'alerte"),
			components.Muted(fmt.Sprintf("%d règles", len(rules))),
			status,
		),
	)

	rows := container.NewVBox()
	for i := range rules {
		r := rules[i]
		state := "désactivée"
		if r.Enabled {
			state = "activée"
		}
		btn := widget.NewButton(
			fmt.Sprintf("%s · %s %s %.2f · %s · %s", r.Name, r.Metric, r.Operator, r.Threshold, state, r.Category),
			func() {
				selected = &r
				status.SetText("Sélection : " + r.Name)
			},
		)
		btn.Importance = widget.LowImportance
		rows.Add(container.NewBorder(nil, nil, components.SeverityBadge(r.Severity), widget.NewLabel(r.Status), btn))
	}
	if len(rules) == 0 {
		rows.Add(components.Muted("Aucune règle"))
	}
	return container.NewBorder(header, nil, nil, nil, container.NewVScroll(rows))
}
