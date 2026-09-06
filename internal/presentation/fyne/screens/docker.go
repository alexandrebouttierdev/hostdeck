package screens

import (
	"context"
	"fmt"
	"strings"
	"time"

	"fyne.io/fyne/v2"
	"fyne.io/fyne/v2/container"
	"fyne.io/fyne/v2/dialog"
	"fyne.io/fyne/v2/widget"
	"github.com/alexandrebouttierdev/hostdeck/internal/application/dto"
	"github.com/alexandrebouttierdev/hostdeck/internal/bootstrap"
	"github.com/alexandrebouttierdev/hostdeck/internal/presentation/fyne/components"
)

// Docker shows containers for a selected (or first Docker-enabled) host.
func Docker(win fyne.Window, services *bootstrap.AppServices, preferredServerID string) fyne.CanvasObject {
	ctx, cancel := context.WithTimeout(context.Background(), 5*time.Second)
	defer cancel()
	hosts, err := services.Servers.GetServers(ctx)
	if err != nil {
		return widget.NewLabel(err.Error())
	}
	var dockerHosts []dto.ServerSummaryDTO
	for _, h := range hosts {
		if h.DockerEnabled {
			dockerHosts = append(dockerHosts, h)
		}
	}
	if len(dockerHosts) == 0 {
		return container.NewVBox(
			components.Title("Docker"),
			components.Muted("Aucun hôte Docker activé. Activez Docker sur un hôte dans Infrastructure."),
		)
	}

	serverID := preferredServerID
	found := false
	names := make([]string, 0, len(dockerHosts))
	byName := map[string]string{}
	for _, h := range dockerHosts {
		names = append(names, h.Name)
		byName[h.Name] = h.ID
		if h.ID == preferredServerID {
			found = true
		}
	}
	if !found {
		serverID = dockerHosts[0].ID
	}
	selectedName := dockerHosts[0].Name
	for n, id := range byName {
		if id == serverID {
			selectedName = n
			break
		}
	}

	hostSelect := widget.NewSelect(names, nil)
	hostSelect.SetSelected(selectedName)

	var body *fyne.Container
	reload := func() {
		sid := byName[hostSelect.Selected]
		if sid == "" {
			sid = serverID
		}
		body.Objects = []fyne.CanvasObject{buildDockerBody(win, services, sid)}
		body.Refresh()
	}
	hostSelect.OnChanged = func(string) { reload() }
	body = container.NewStack(buildDockerBody(win, services, serverID))

	header := container.NewBorder(nil, nil, nil, hostSelect,
		container.NewVBox(components.Title("Docker"), components.Muted("Conteneurs · actions · journaux")),
	)
	return container.NewBorder(header, nil, nil, nil, body)
}

func buildDockerBody(win fyne.Window, services *bootstrap.AppServices, serverID string) fyne.CanvasObject {
	ctx, cancel := context.WithTimeout(context.Background(), 8*time.Second)
	defer cancel()
	list, err := services.Docker.ListContainers(ctx, serverID)
	if err != nil {
		return widget.NewLabel(err.Error())
	}
	info, _ := services.Docker.GetInfo(ctx, serverID)
	infoLine := components.Muted(fmt.Sprintf("%s · %d conteneurs · %d en cours", info.DockerVersion, info.Containers, info.ContainersRunning))

	selected := ""
	logsView := widget.NewMultiLineEntry()
	logsView.SetText("Sélectionnez un conteneur pour voir les journaux.")
	logsView.Wrapping = fyne.TextWrapWord

	rows := container.NewVBox()
	for _, c := range list {
		item := c
		btn := widget.NewButton(fmt.Sprintf("%s · %s · %s · CPU %.0f%% · RAM %.0f%%", item.Name, item.Image, item.Status, item.CPUPercent, item.MemoryPercent), func() {
			selected = item.ID
			ctx, cancel := context.WithTimeout(context.Background(), 8*time.Second)
			defer cancel()
			lines, err := services.Docker.Logs(ctx, serverID, item.ID, 80)
			if err != nil {
				logsView.SetText(err.Error())
				return
			}
			logsView.SetText(strings.Join(lines, "\n"))
		})
		btn.Importance = widget.LowImportance
		rows.Add(container.NewBorder(nil, nil, components.StatusDot(mapDockerStatus(item.Status)), nil, btn))
	}
	if len(list) == 0 {
		rows.Add(components.Muted("Aucun conteneur"))
	}

	act := func(label string, fn func(context.Context, string, string) error) *widget.Button {
		return components.GhostButton(label, func() {
			if selected == "" {
				dialog.ShowInformation("Docker", "Sélectionnez un conteneur", win)
				return
			}
			ctx, cancel := context.WithTimeout(context.Background(), 20*time.Second)
			defer cancel()
			if err := fn(ctx, serverID, selected); err != nil {
				dialog.ShowError(err, win)
				return
			}
			dialog.ShowInformation("Docker", label+" : OK", win)
		})
	}

	actions := container.NewHBox(
		act("Démarrer", services.Docker.Start),
		act("Arrêter", services.Docker.Stop),
		act("Redémarrer", services.Docker.Restart),
	)

	left := components.Panel("Conteneurs", container.NewVScroll(rows))
	right := components.Panel("Journaux", container.NewBorder(actions, nil, nil, nil, logsView))
	return container.NewBorder(infoLine, nil, nil, nil, container.NewHSplit(left, right))
}

func mapDockerStatus(status string) string {
	switch strings.ToLower(status) {
	case "running":
		return "online"
	case "exited", "dead":
		return "offline"
	case "restarting", "paused":
		return "warning"
	default:
		return status
	}
}
