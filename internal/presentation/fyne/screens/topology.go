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

func Topology(services *bootstrap.AppServices) fyne.CanvasObject {
	ctx, cancel := context.WithTimeout(context.Background(), 5*time.Second)
	defer cancel()
	hosts, err := services.Servers.GetServers(ctx)
	if err != nil {
		return widget.NewLabel(err.Error())
	}
	groups := map[string][]string{}
	for _, h := range hosts {
		g := h.Group
		if g == "" {
			g = "default"
		}
		groups[g] = append(groups[g], fmt.Sprintf("%s (%s)", h.Name, h.Status))
	}
	cols := container.NewHBox()
	for g, names := range groups {
		box := container.NewVBox(components.Title(g))
		for _, n := range names {
			box.Add(widget.NewLabel(n))
		}
		cols.Add(components.Panel(g, box))
	}
	return container.NewBorder(components.Title("Topologie"), nil, nil, nil, container.NewHScroll(cols))
}
