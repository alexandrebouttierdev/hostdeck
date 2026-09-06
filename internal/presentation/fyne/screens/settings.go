package screens

import (
	"context"
	"fmt"
	"strconv"
	"time"

	"fyne.io/fyne/v2"
	"fyne.io/fyne/v2/container"
	"fyne.io/fyne/v2/widget"
	"github.com/alexandrebouttierdev/hostdeck/internal/bootstrap"
	"github.com/alexandrebouttierdev/hostdeck/internal/presentation/fyne/components"
)

func Settings(services *bootstrap.AppServices) fyne.CanvasObject {
	ctx, cancel := context.WithTimeout(context.Background(), 5*time.Second)
	defer cancel()
	cfg, err := services.Settings.Get(ctx)
	if err != nil {
		return widget.NewLabel(err.Error())
	}
	name := widget.NewEntry()
	name.SetText(cfg.InstanceName)
	desc := widget.NewMultiLineEntry()
	desc.SetText(cfg.Description)
	tz := widget.NewEntry()
	tz.SetText(cfg.Timezone)
	lang := widget.NewEntry()
	lang.SetText(cfg.Language)
	interval := widget.NewEntry()
	interval.SetText(strconv.Itoa(cfg.DefaultIntervalSeconds))
	theme := widget.NewEntry()
	theme.SetText(cfg.Theme)
	status := widget.NewLabel("")
	save := components.PrimaryButton("Enregistrer", func() {
		cfg.InstanceName = name.Text
		cfg.Description = desc.Text
		cfg.Timezone = tz.Text
		cfg.Language = lang.Text
		if v, err := strconv.Atoi(interval.Text); err == nil {
			cfg.DefaultIntervalSeconds = v
		}
		cfg.Theme = theme.Text
		if _, err := services.Settings.Update(context.Background(), cfg); err != nil {
			status.SetText("Erreur : " + err.Error())
			return
		}
		status.SetText(fmt.Sprintf("Enregistré à %s", time.Now().Format("15:04:05")))
	})
	form := container.NewVBox(
		components.Title("Paramètres"),
		widget.NewForm(
			widget.NewFormItem("Nom de l'instance", name),
			widget.NewFormItem("Description", desc),
			widget.NewFormItem("Fuseau horaire", tz),
			widget.NewFormItem("Langue", lang),
			widget.NewFormItem("Intervalle (s)", interval),
			widget.NewFormItem("Thème", theme),
		),
		save, status,
	)
	return container.NewVScroll(form)
}
