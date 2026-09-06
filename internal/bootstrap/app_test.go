package bootstrap_test

import (
	"context"
	"path/filepath"
	"testing"

	"github.com/alexandrebouttierdev/hostdeck/internal/bootstrap"
)

func TestOpenWiresAndSeeds(t *testing.T) {
	ctx := context.Background()
	dir := t.TempDir()
	app, err := bootstrap.Open(ctx, bootstrap.Options{
		DBPath:            filepath.Join(dir, "hostdeck.db"),
		SeedDemo:          true,
		UseKeyring:        false,
		UseDemoDocker:     true,
		LogLevel:          "error",
		AutoStartFleet:    false,
		AutoStartFleetSet: true,
	})
	if err != nil {
		t.Fatal(err)
	}
	defer app.Close()

	list, err := app.Servers.GetServers(ctx)
	if err != nil {
		t.Fatal(err)
	}
	if len(list) < 12 {
		t.Fatalf("expected seeded servers, got %d", len(list))
	}
	ov, err := app.Overview.Build(ctx)
	if err != nil {
		t.Fatal(err)
	}
	if ov.TotalHosts != len(list) {
		t.Fatalf("overview hosts %d != %d", ov.TotalHosts, len(list))
	}
}
