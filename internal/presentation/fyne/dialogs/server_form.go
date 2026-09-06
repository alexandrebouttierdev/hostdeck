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

// ShowAddServerDialog opens a form to create a monitored host.
func ShowAddServerDialog(win fyne.Window, services *bootstrap.AppServices, onDone func()) {
	name := widget.NewEntry()
	host := widget.NewEntry()
	port := widget.NewEntry()
	port.SetText("22")
	user := widget.NewEntry()
	user.SetText("root")
	secret := widget.NewPasswordEntry()
	group := widget.NewEntry()
	env := widget.NewEntry()
	role := widget.NewEntry()
	mode := widget.NewSelect([]string{"direct", "jump"}, nil)
	mode.SetSelected("direct")
	auth := widget.NewSelect([]string{"key", "password", "agent"}, nil)
	auth.SetSelected("key")
	mon := widget.NewCheck("Surveillance activée", nil)
	mon.SetChecked(true)
	dock := widget.NewCheck("Docker activé", nil)

	form := dialog.NewForm("Ajouter un hôte", "Enregistrer", "Annuler", []*widget.FormItem{
		widget.NewFormItem("Nom", name),
		widget.NewFormItem("Hôte / IP", host),
		widget.NewFormItem("Port", port),
		widget.NewFormItem("Utilisateur", user),
		widget.NewFormItem("Secret (clé/mdp)", secret),
		widget.NewFormItem("Mode", mode),
		widget.NewFormItem("Auth", auth),
		widget.NewFormItem("Groupe", group),
		widget.NewFormItem("Environnement", env),
		widget.NewFormItem("Rôle", role),
		widget.NewFormItem("", mon),
		widget.NewFormItem("", dock),
	}, func(ok bool) {
		if !ok {
			return
		}
		p, _ := strconv.Atoi(port.Text)
		if p == 0 {
			p = 22
		}
		ctx, cancel := context.WithTimeout(context.Background(), 10*time.Second)
		defer cancel()
		_, err := services.Servers.Add(ctx, dto.CreateServerDTO{
			Name: name.Text, Host: host.Text, Port: p, Username: user.Text,
			ConnectionMode: mode.Selected, AuthMethod: auth.Selected,
			CredentialSecret: secret.Text, Group: group.Text, Environment: env.Text, Role: role.Text,
			MonitoringEnabled: mon.Checked, DockerEnabled: dock.Checked, IntervalSeconds: 60,
		})
		if err != nil {
			dialog.ShowError(err, win)
			return
		}
		if onDone != nil {
			onDone()
		}
	}, win)
	form.Resize(fyne.NewSize(480, 520))
	form.Show()
}

// ShowEditServerDialog edits an existing host.
func ShowEditServerDialog(win fyne.Window, services *bootstrap.AppServices, id string, onDone func()) {
	ctx, cancel := context.WithTimeout(context.Background(), 5*time.Second)
	defer cancel()
	details, err := services.Servers.GetServerDetails(ctx, id)
	if err != nil {
		dialog.ShowError(err, win)
		return
	}
	name := widget.NewEntry()
	name.SetText(details.Name)
	host := widget.NewEntry()
	host.SetText(details.Host)
	port := widget.NewEntry()
	port.SetText(strconv.Itoa(details.Port))
	user := widget.NewEntry()
	user.SetText(details.Username)
	secret := widget.NewPasswordEntry()
	group := widget.NewEntry()
	group.SetText(details.Group)
	env := widget.NewEntry()
	env.SetText(details.Environment)
	role := widget.NewEntry()
	role.SetText(details.Role)
	mode := widget.NewSelect([]string{"direct", "jump"}, nil)
	mode.SetSelected(details.ConnectionMode)
	if mode.Selected == "" {
		mode.SetSelected("direct")
	}
	auth := widget.NewSelect([]string{"key", "password", "agent"}, nil)
	auth.SetSelected(details.AuthMethod)
	if auth.Selected == "" {
		auth.SetSelected("key")
	}
	mon := widget.NewCheck("Surveillance activée", nil)
	mon.SetChecked(details.MonitoringEnabled)
	dock := widget.NewCheck("Docker activé", nil)
	dock.SetChecked(details.DockerEnabled)

	form := dialog.NewForm("Modifier l'hôte", "Enregistrer", "Annuler", []*widget.FormItem{
		widget.NewFormItem("Nom", name),
		widget.NewFormItem("Hôte / IP", host),
		widget.NewFormItem("Port", port),
		widget.NewFormItem("Utilisateur", user),
		widget.NewFormItem("Nouveau secret (optionnel)", secret),
		widget.NewFormItem("Mode", mode),
		widget.NewFormItem("Auth", auth),
		widget.NewFormItem("Groupe", group),
		widget.NewFormItem("Environnement", env),
		widget.NewFormItem("Rôle", role),
		widget.NewFormItem("", mon),
		widget.NewFormItem("", dock),
	}, func(ok bool) {
		if !ok {
			return
		}
		p, _ := strconv.Atoi(port.Text)
		ctx, cancel := context.WithTimeout(context.Background(), 10*time.Second)
		defer cancel()
		_, err := services.Servers.Update(ctx, dto.UpdateServerDTO{
			ID: id, Name: name.Text, Host: host.Text, Port: p, Username: user.Text,
			ConnectionMode: mode.Selected, AuthMethod: auth.Selected,
			CredentialSecret: secret.Text, Group: group.Text, Environment: env.Text, Role: role.Text,
			MonitoringEnabled: mon.Checked, DockerEnabled: dock.Checked,
			IntervalSeconds: details.IntervalSeconds, Status: details.Status, Tags: details.Tags,
		})
		if err != nil {
			dialog.ShowError(err, win)
			return
		}
		if onDone != nil {
			onDone()
		}
	}, win)
	form.Resize(fyne.NewSize(480, 520))
	form.Show()
}

// ConfirmDeleteServer asks then deletes.
func ConfirmDeleteServer(win fyne.Window, services *bootstrap.AppServices, id, name string, onDone func()) {
	dialog.ShowConfirm("Supprimer l'hôte", fmt.Sprintf("Supprimer « %s » ?", name), func(ok bool) {
		if !ok {
			return
		}
		ctx, cancel := context.WithTimeout(context.Background(), 5*time.Second)
		defer cancel()
		if err := services.Servers.Delete(ctx, id); err != nil {
			dialog.ShowError(err, win)
			return
		}
		if onDone != nil {
			onDone()
		}
	}, win)
}

// TestServerConnection runs SSH connectivity test and shows result.
func TestServerConnection(win fyne.Window, services *bootstrap.AppServices, id string) {
	ctx, cancel := context.WithTimeout(context.Background(), 20*time.Second)
	defer cancel()
	res, err := services.Servers.TestConnection(ctx, dto.TestConnectionRequestDTO{ServerID: id})
	if err != nil {
		dialog.ShowError(err, win)
		return
	}
	msg := res.Message
	if res.Success {
		msg = fmt.Sprintf("Connexion OK (%d ms)\n%s", res.LatencyMs, res.Message)
		dialog.ShowInformation("Test de connexion", msg, win)
		return
	}
	dialog.ShowError(fmt.Errorf("%s", msg), win)
}
