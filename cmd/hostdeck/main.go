package main

import (
	"context"
	"flag"
	"fmt"
	"os"
	"path/filepath"

	"fyne.io/fyne/v2"
	"fyne.io/fyne/v2/app"
	"github.com/alexandrebouttierdev/hostdeck/internal/bootstrap"
	uiapp "github.com/alexandrebouttierdev/hostdeck/internal/presentation/fyne/app"
	hdtheme "github.com/alexandrebouttierdev/hostdeck/internal/presentation/fyne/theme"
)

func main() {
	dbPath := flag.String("db", "", "chemin SQLite (défaut: ~/.local/share/hostdeck/hostdeck.db)")
	seed := flag.Bool("seed", true, "injecter les données de démonstration si besoin")
	flag.Parse()

	ctx := context.Background()
	opts := bootstrap.Options{
		DBPath:        *dbPath,
		SeedDemo:      *seed,
		UseKeyring:    false,
		UseDemoDocker: true,
		LogLevel:      "info",
	}
	services, err := bootstrap.Open(ctx, opts)
	if err != nil {
		fmt.Fprintf(os.Stderr, "bootstrap: %v\n", err)
		os.Exit(1)
	}
	defer services.Close()

	a := app.NewWithID("dev.hostdeck.app")
	a.Settings().SetTheme(&hdtheme.HostDeckTheme{})
	w := a.NewWindow("HostDeck")
	w.Resize(fyne.NewSize(1440, 900))
	shell := uiapp.NewShell(w, services)
	w.SetContent(shell.CanvasObject())
	if wd, err := os.Getwd(); err == nil {
		_ = filepath.Walk(wd, func(path string, info os.FileInfo, err error) error { return nil })
	}
	w.ShowAndRun()
}
