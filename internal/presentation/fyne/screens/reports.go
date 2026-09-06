package screens

import (
	"context"
	"fmt"
	"time"

	"fyne.io/fyne/v2"
	"fyne.io/fyne/v2/container"
	"fyne.io/fyne/v2/widget"
	"github.com/alexandrebouttierdev/hostdeck/internal/bootstrap"
	"github.com/alexandrebouttierdev/hostdeck/internal/presentation/fyne/charts"
	"github.com/alexandrebouttierdev/hostdeck/internal/presentation/fyne/components"
	hdtheme "github.com/alexandrebouttierdev/hostdeck/internal/presentation/fyne/theme"
)

func Reports(services *bootstrap.AppServices) fyne.CanvasObject {
	ctx, cancel := context.WithTimeout(context.Background(), 5*time.Second)
	defer cancel()
	ov, err := services.Overview.Build(ctx)
	if err != nil {
		return widget.NewLabel(err.Error())
	}
	header := components.Title("Rapports")
	kpis := container.NewGridWithColumns(4,
		components.KPI("Disponibilité", components.FormatPct(ov.AvailabilityPct), "période courante", hdtheme.ColorOnline),
		components.KPI("Incidents", fmt.Sprintf("%d", ov.ActiveIncidents), "actifs", hdtheme.ColorCritical),
		components.KPI("CPU moyen", components.FormatPct(ov.GlobalCPUPercent), "flotte", hdtheme.ColorSeriesBlue),
		components.KPI("Mémoire moyenne", components.FormatPct(ov.GlobalMemoryPercent), "flotte", hdtheme.ColorSeriesGreen),
	)
	pts := make([]float64, 0, len(ov.TopHosts))
	for _, h := range ov.TopHosts {
		pts = append(pts, h.CPUPercent)
	}
	chart := components.Panel("Charge CPU des hôtes prioritaires", charts.NewTimeSeriesChart("CPU", []charts.Series{{Name: "CPU", Color: hdtheme.ColorSeriesBlue, Points: pts}}))
	return container.NewVBox(header, components.Muted("Synthèse opérationnelle"), kpis, chart)
}
