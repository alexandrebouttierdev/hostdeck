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

func HostDetails(services *bootstrap.AppServices, serverID string) fyne.CanvasObject {
	if serverID == "" {
		return components.Muted("Sélectionnez un hôte depuis Infrastructure.")
	}
	ctx, cancel := context.WithTimeout(context.Background(), 5*time.Second)
	defer cancel()
	details, err := services.Servers.GetServerDetails(ctx, serverID)
	if err != nil {
		return widget.NewLabel(err.Error())
	}
	hist, _ := services.Monitoring.GetHistory(ctx, dto.MetricHistoryRequestDTO{ServerID: serverID, Range: "1h", Limit: 120})
	cpu, mem, net := []float64{}, []float64{}, []float64{}
	for _, p := range hist.Points {
		cpu = append(cpu, p.CPUTotalPercent)
		mem = append(mem, p.MemoryUsedPercent)
		net = append(net, p.NetworkRXBytesPerSec/1e6)
	}
	header := container.NewVBox(
		components.Title(details.Name),
		components.Muted(fmt.Sprintf("%s · %s · %s", details.Host, details.OSName, details.Status)),
	)
	meta := container.NewGridWithColumns(4,
		components.KPI("CPU", components.FormatPct(details.CPUPercent), "actuel", hdtheme.ColorSeriesBlue),
		components.KPI("Mémoire", components.FormatPct(details.MemoryPercent), "actuel", hdtheme.ColorSeriesGreen),
		components.KPI("Disque", components.FormatPct(details.DiskPercent), "actuel", hdtheme.ColorSeriesPurple),
		components.KPI("Load", components.FormatFloat(details.Load1), "1 min", hdtheme.ColorSeriesOrange),
	)
	chartsRow := container.NewGridWithColumns(2,
		components.Panel("CPU", charts.NewTimeSeriesChart("CPU", []charts.Series{{Name: "CPU", Color: hdtheme.ColorSeriesBlue, Points: cpu}})),
		components.Panel("Mémoire", charts.NewTimeSeriesChart("Mémoire", []charts.Series{{Name: "Mémoire", Color: hdtheme.ColorSeriesGreen, Points: mem}})),
	)
	netPanel := components.Panel("Réseau RX (MB/s)", charts.NewTimeSeriesChart("Réseau", []charts.Series{{Name: "RX", Color: hdtheme.ColorSeriesCyan, Points: net}}))
	info := components.Panel("Informations", container.NewVBox(
		widget.NewLabel(fmt.Sprintf("Groupe : %s", details.Group)),
		widget.NewLabel(fmt.Sprintf("Environnement : %s", details.Environment)),
		widget.NewLabel(fmt.Sprintf("Rôle : %s", details.Role)),
		widget.NewLabel(fmt.Sprintf("Mode : %s", details.ConnectionMode)),
		widget.NewLabel(fmt.Sprintf("Docker : %v", details.DockerEnabled)),
	))
	return container.NewVScroll(container.NewVBox(header, meta, chartsRow, container.NewGridWithColumns(2, netPanel, info)))
}
