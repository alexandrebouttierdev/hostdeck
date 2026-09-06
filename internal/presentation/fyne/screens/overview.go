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
	"github.com/alexandrebouttierdev/hostdeck/internal/presentation/fyne/charts"
	"github.com/alexandrebouttierdev/hostdeck/internal/presentation/fyne/components"
	hdtheme "github.com/alexandrebouttierdev/hostdeck/internal/presentation/fyne/theme"
)

func Overview(services *bootstrap.AppServices) fyne.CanvasObject {
	ctx, cancel := context.WithTimeout(context.Background(), 5*time.Second)
	defer cancel()
	ov, err := services.Overview.Build(ctx)
	if err != nil {
		return widget.NewLabel("Impossible de charger la vue d'ensemble : " + err.Error())
	}

	header := container.NewVBox(
		components.Title("Bonjour — état de votre infrastructure"),
		components.Muted("Supervision multi-hôtes locale · Go + Fyne"),
	)
	kpis := container.NewGridWithColumns(4,
		components.KPI("Hôtes surveillés", fmt.Sprintf("%d", ov.TotalHosts),
			fmt.Sprintf("%d OK · %d warn · %d problèmes", ov.OnlineHosts, ov.WarningHosts, ov.ProblemHosts), hdtheme.ColorAccent),
		components.KPI("Incidents actifs", fmt.Sprintf("%d", ov.ActiveIncidents),
			fmt.Sprintf("%d critiques · %d majeurs", ov.CriticalIncidents, ov.HighIncidents), hdtheme.ColorCritical),
		components.KPI("Règles d'alerte", fmt.Sprintf("%d", ov.OpenAlertRules), "ouvertes", hdtheme.ColorWarning),
		components.KPI("Disponibilité", components.FormatPct(ov.AvailabilityPct), "flotte globale", hdtheme.ColorOnline),
	)
	gauges := container.NewGridWithColumns(4,
		components.KPI("CPU global", components.FormatPct(ov.GlobalCPUPercent), "moyenne", hdtheme.ColorSeriesBlue),
		components.KPI("Mémoire", components.FormatPct(ov.GlobalMemoryPercent), "moyenne", hdtheme.ColorSeriesGreen),
		components.KPI("Stockage", components.FormatPct(ov.GlobalDiskPercent), "moyenne", hdtheme.ColorSeriesPurple),
		components.KPI("Collecte", map[bool]string{true: "Active", false: "Inactive"}[ov.CollectionActive], time.Now().Format("15:04:05"), hdtheme.ColorAccent),
	)

	cpuPts := flattenSparks(ov.TopHosts, true)
	memPts := flattenSparks(ov.TopHosts, false)
	chartsRow := container.NewGridWithColumns(2,
		components.Panel("Utilisation CPU globale", charts.NewTimeSeriesChart("CPU", []charts.Series{{Name: "CPU", Color: hdtheme.ColorSeriesBlue, Points: cpuPts}})),
		components.Panel("Utilisation mémoire globale", charts.NewTimeSeriesChart("Mémoire", []charts.Series{{Name: "Mémoire", Color: hdtheme.ColorSeriesGreen, Points: memPts}})),
	)

	incBox := container.NewVBox()
	for i, inc := range ov.RecentIncidents {
		if i >= 8 {
			break
		}
		incBox.Add(container.NewBorder(nil, nil,
			components.SeverityBadge(inc.Severity),
			components.Muted(fmt.Sprintf("%d min", inc.DurationSecs/60)),
			widget.NewLabel(fmt.Sprintf("%s — %s · %s", inc.DisplayID, inc.ServerName, inc.Problem)),
		))
	}
	if len(ov.RecentIncidents) == 0 {
		incBox.Add(components.Muted("Aucun incident récent"))
	}

	topBox := container.NewVBox()
	for i, h := range ov.TopHosts {
		if i >= 8 {
			break
		}
		topBox.Add(container.NewBorder(nil, nil,
			components.StatusDot(h.Status),
			widget.NewLabel(components.FormatPct(h.CPUPercent)),
			widget.NewLabel(fmt.Sprintf("%s (%s)", h.Name, h.Host)),
		))
	}

	bottom := container.NewGridWithColumns(2,
		components.Panel("Incidents récents", container.NewVScroll(incBox)),
		components.Panel("Hôtes les plus sollicités", container.NewVScroll(topBox)),
	)
	return container.NewVScroll(container.NewVBox(header, kpis, gauges, chartsRow, bottom))
}

func flattenSparks(hosts []dto.ServerSummaryDTO, cpu bool) []float64 {
	out := make([]float64, 0, 48)
	for _, h := range hosts {
		if cpu {
			out = append(out, h.CPUSparkline...)
		} else {
			out = append(out, h.MemorySparkline...)
		}
	}
	if len(out) == 0 {
		return []float64{12, 18, 22, 30, 28, 35, 40, 38, 42, 37}
	}
	if len(out) > 60 {
		out = out[:60]
	}
	return out
}
