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

func LiveData(services *bootstrap.AppServices, serverID string) fyne.CanvasObject {
	ctx, cancel := context.WithTimeout(context.Background(), 5*time.Second)
	defer cancel()
	hosts, err := services.Servers.GetServers(ctx)
	if err != nil {
		return widget.NewLabel(err.Error())
	}
	if serverID == "" && len(hosts) > 0 {
		serverID = hosts[0].ID
	}
	if serverID == "" {
		return components.Muted("Aucun hôte disponible")
	}
	latest, err := services.Monitoring.GetLatest(ctx, serverID)
	if err != nil {
		latest = dto.LatestMetricDTO{}
	}
	hist, _ := services.Monitoring.GetHistory(ctx, dto.MetricHistoryRequestDTO{ServerID: serverID, Range: "15m", Limit: 90})
	cpu, mem, load := []float64{}, []float64{}, []float64{}
	for _, p := range hist.Points {
		cpu = append(cpu, p.CPUTotalPercent)
		mem = append(mem, p.MemoryUsedPercent)
		load = append(load, p.Load1)
	}
	header := container.NewVBox(
		components.Title("Données en direct"),
		components.Muted(fmt.Sprintf("Hôte %s · CPU %.1f%% · Mémoire %.1f%% · Load %.2f", serverID, latest.CPUTotalPercent, latest.MemoryUsedPercent, latest.Load1)),
	)
	grid := container.NewGridWithColumns(1,
		components.Panel("CPU", charts.NewTimeSeriesChart("CPU", []charts.Series{{Name: "CPU", Color: hdtheme.ColorSeriesBlue, Points: cpu}})),
		components.Panel("Mémoire", charts.NewTimeSeriesChart("Mémoire", []charts.Series{{Name: "Mémoire", Color: hdtheme.ColorSeriesGreen, Points: mem}})),
		components.Panel("Load", charts.NewTimeSeriesChart("Load", []charts.Series{{Name: "Load1", Color: hdtheme.ColorSeriesOrange, Points: load}})),
	)
	return container.NewBorder(header, nil, nil, nil, container.NewVScroll(grid))
}
