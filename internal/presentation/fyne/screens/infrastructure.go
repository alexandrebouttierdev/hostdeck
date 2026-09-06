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
	"github.com/alexandrebouttierdev/hostdeck/internal/presentation/fyne/dialogs"
	hdtheme "github.com/alexandrebouttierdev/hostdeck/internal/presentation/fyne/theme"
)

// Infrastructure renders the dense hosts table with CRUD actions.
func Infrastructure(win fyne.Window, services *bootstrap.AppServices, onOpen func(string)) fyne.CanvasObject {
	var root *fyne.Container
	var reload func()
	reload = func() {
		root.Objects = []fyne.CanvasObject{buildInfrastructure(win, services, onOpen, reload)}
		root.Refresh()
	}
	root = container.NewStack(buildInfrastructure(win, services, onOpen, reload))
	return root
}

func buildInfrastructure(win fyne.Window, services *bootstrap.AppServices, onOpen func(string), reload func()) fyne.CanvasObject {
	ctx, cancel := context.WithTimeout(context.Background(), 5*time.Second)
	defer cancel()
	hosts, err := services.Servers.GetServers(ctx)
	if err != nil {
		return widget.NewLabel(err.Error())
	}
	online, problem := 0, 0
	for _, h := range hosts {
		switch h.Status {
		case "online":
			online++
		case "warning", "maintenance":
		default:
			problem++
		}
	}

	selectedID := ""
	selectedName := ""
	status := widget.NewLabel("")

	addBtn := components.PrimaryButton("Ajouter", func() {
		dialogs.ShowAddServerDialog(win, services, reload)
	})
	editBtn := components.GhostButton("Modifier", func() {
		if selectedID == "" {
			status.SetText("Sélectionnez un hôte")
			return
		}
		dialogs.ShowEditServerDialog(win, services, selectedID, reload)
	})
	delBtn := components.GhostButton("Supprimer", func() {
		if selectedID == "" {
			status.SetText("Sélectionnez un hôte")
			return
		}
		dialogs.ConfirmDeleteServer(win, services, selectedID, selectedName, reload)
	})
	testBtn := components.GhostButton("Tester SSH", func() {
		if selectedID == "" {
			status.SetText("Sélectionnez un hôte")
			return
		}
		dialogs.TestServerConnection(win, services, selectedID)
	})

	header := container.NewBorder(nil, nil, nil,
		container.NewHBox(addBtn, editBtn, delBtn, testBtn),
		container.NewVBox(
			components.Title("Infrastructure"),
			components.Muted(fmt.Sprintf("%d hôtes · %d opérationnels · %d problèmes", len(hosts), online, problem)),
			status,
		),
	)

	table := widget.NewTable(
		func() (int, int) { return len(hosts) + 1, 7 },
		func() fyne.CanvasObject {
			return container.NewHBox(widget.NewLabel("cell"), charts.NewSparkline(nil, hdtheme.ColorSeriesBlue))
		},
		func(id widget.TableCellID, obj fyne.CanvasObject) {
			box := obj.(*fyne.Container)
			if id.Row == 0 {
				headers := []string{"État", "Hôte", "Adresse", "OS", "CPU", "Mémoire", "Charge"}
				box.Objects = []fyne.CanvasObject{widget.NewLabel(headers[id.Col])}
				box.Refresh()
				return
			}
			h := hosts[id.Row-1]
			switch id.Col {
			case 0:
				box.Objects = []fyne.CanvasObject{components.StatusDot(h.Status)}
			case 1:
				name := h.Name
				hid := h.ID
				box.Objects = []fyne.CanvasObject{widget.NewButton(name, func() {
					selectedID = hid
					selectedName = name
					status.SetText("Sélection : " + name)
					if onOpen != nil {
						onOpen(hid)
					}
				})}
			case 2:
				box.Objects = []fyne.CanvasObject{widget.NewLabel(h.Host)}
			case 3:
				box.Objects = []fyne.CanvasObject{widget.NewLabel(h.OSName)}
			case 4:
				box.Objects = []fyne.CanvasObject{container.NewBorder(nil, nil, nil, widget.NewLabel(components.FormatPct(h.CPUPercent)), charts.NewSparkline(h.CPUSparkline, hdtheme.ColorSeriesBlue))}
			case 5:
				box.Objects = []fyne.CanvasObject{container.NewBorder(nil, nil, nil, widget.NewLabel(components.FormatPct(h.MemoryPercent)), charts.NewSparkline(h.MemorySparkline, hdtheme.ColorSeriesGreen))}
			case 6:
				box.Objects = []fyne.CanvasObject{widget.NewLabel(components.FormatFloat(h.Load1))}
			}
			box.Refresh()
		},
	)
	table.SetColumnWidth(0, 48)
	table.SetColumnWidth(1, 170)
	table.SetColumnWidth(2, 130)
	table.SetColumnWidth(3, 140)
	table.SetColumnWidth(4, 150)
	table.SetColumnWidth(5, 150)
	table.SetColumnWidth(6, 70)
	return container.NewBorder(header, nil, nil, nil, table)
}
