package screens

import (
	"context"
	"fmt"
	"time"

	"fyne.io/fyne/v2"
	"fyne.io/fyne/v2/container"
	"fyne.io/fyne/v2/widget"
	"github.com/alexandrebouttierdev/hostdeck/internal/bootstrap"
	"github.com/alexandrebouttierdev/hostdeck/internal/presentation/fyne/components"
)

func Alerts(services *bootstrap.AppServices) fyne.CanvasObject {
	ctx, cancel := context.WithTimeout(context.Background(), 5*time.Second)
	defer cancel()
	rules, err := services.Alerts.List(ctx)
	if err != nil {
		return widget.NewLabel(err.Error())
	}
	header := container.NewVBox(components.Title("Règles d'alerte"), components.Muted(fmt.Sprintf("%d règles", len(rules))))
	rows := container.NewVBox()
	for _, r := range rules {
		state := "désactivée"
		if r.Enabled {
			state = "activée"
		}
		rows.Add(container.NewBorder(nil, nil, components.SeverityBadge(r.Severity), widget.NewLabel(r.Status),
			widget.NewLabel(fmt.Sprintf("%s · %s %s %.2f · %s · %s", r.Name, r.Metric, r.Operator, r.Threshold, state, r.Category))))
	}
	if len(rules) == 0 {
		rows.Add(components.Muted("Aucune règle"))
	}
	return container.NewBorder(header, nil, nil, nil, container.NewVScroll(rows))
}
