package dialogs

import (
	"context"
	"fmt"
	"strconv"
	"time"

	"fyne.io/fyne/v2"
	"fyne.io/fyne/v2/dialog"
	"fyne.io/fyne/v2/widget"
	"github.com/alexandrebouttierdev/hostdeck/internal/application/dto"
	"github.com/alexandrebouttierdev/hostdeck/internal/bootstrap"
)

func ShowAlertRuleDialog(win fyne.Window, services *bootstrap.AppServices, existing *dto.AlertRuleDTO, onDone func()) {
	title := "Nouvelle règle d'alerte"
	rule := dto.AlertRuleDTO{
		Enabled: true, Metric: "cpu", Operator: ">", Threshold: 90,
		DurationSeconds: 300, Severity: "high", CooldownSeconds: 600, Scope: "all", Category: "system",
	}
	if existing != nil {
		title = "Modifier la règle"
		rule = *existing
	}
	name := widget.NewEntry()
	name.SetText(rule.Name)
	metric := widget.NewSelect([]string{"cpu", "memory", "disk", "load"}, nil)
	metric.SetSelected(rule.Metric)
	op := widget.NewSelect([]string{">", ">=", "<", "<=", "=="}, nil)
	op.SetSelected(rule.Operator)
	threshold := widget.NewEntry()
	threshold.SetText(fmt.Sprintf("%g", rule.Threshold))
	duration := widget.NewEntry()
	duration.SetText(strconv.Itoa(rule.DurationSeconds))
	severity := widget.NewSelect([]string{"information", "warning", "average", "high", "critical"}, nil)
	severity.SetSelected(rule.Severity)
	cooldown := widget.NewEntry()
	cooldown.SetText(strconv.Itoa(rule.CooldownSeconds))
	scope := widget.NewEntry()
	scope.SetText(rule.Scope)
	category := widget.NewEntry()
	category.SetText(rule.Category)
	enabled := widget.NewCheck("Activée", nil)
	enabled.SetChecked(rule.Enabled)

	form := dialog.NewForm(title, "Enregistrer", "Annuler", []*widget.FormItem{
		widget.NewFormItem("Nom", name),
		widget.NewFormItem("Métrique", metric),
		widget.NewFormItem("Opérateur", op),
		widget.NewFormItem("Seuil", threshold),
		widget.NewFormItem("Durée (s)", duration),
		widget.NewFormItem("Sévérité", severity),
		widget.NewFormItem("Cooldown (s)", cooldown),
		widget.NewFormItem("Périmètre", scope),
		widget.NewFormItem("Catégorie", category),
		widget.NewFormItem("", enabled),
	}, func(ok bool) {
		if !ok {
			return
		}
		th, _ := strconv.ParseFloat(threshold.Text, 64)
		dur, _ := strconv.Atoi(duration.Text)
		cd, _ := strconv.Atoi(cooldown.Text)
		in := dto.AlertRuleDTO{
			ID: rule.ID, Name: name.Text, Enabled: enabled.Checked, Metric: metric.Selected,
			Operator: op.Selected, Threshold: th, DurationSeconds: dur, Severity: severity.Selected,
			CooldownSeconds: cd, Scope: scope.Text, Category: category.Text, Expression: rule.Expression,
			Status: rule.Status, CreatedAt: rule.CreatedAt, UpdatedAt: rule.UpdatedAt,
		}
		ctx, cancel := context.WithTimeout(context.Background(), 5*time.Second)
		defer cancel()
		var err error
		if existing == nil {
			_, err = services.Alerts.Create(ctx, in)
		} else {
			_, err = services.Alerts.Update(ctx, in)
		}
		if err != nil {
			dialog.ShowError(err, win)
			return
		}
		if onDone != nil {
			onDone()
		}
	}, win)
	form.Resize(fyne.NewSize(460, 480))
	form.Show()
}

func ConfirmDeleteAlert(win fyne.Window, services *bootstrap.AppServices, id, name string, onDone func()) {
	dialog.ShowConfirm("Supprimer la règle", fmt.Sprintf("Supprimer « %s » ?", name), func(ok bool) {
		if !ok {
			return
		}
		ctx, cancel := context.WithTimeout(context.Background(), 5*time.Second)
		defer cancel()
		if err := services.Alerts.Delete(ctx, id); err != nil {
			dialog.ShowError(err, win)
			return
		}
		if onDone != nil {
			onDone()
		}
	}, win)
}
