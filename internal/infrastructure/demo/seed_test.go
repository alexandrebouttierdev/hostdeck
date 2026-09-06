package demo_test

import (
	"context"
	"testing"

	"github.com/alexandrebouttierdev/hostdeck/internal/infrastructure/demo"
	dockerruntime "github.com/alexandrebouttierdev/hostdeck/internal/infrastructure/docker"
	"github.com/alexandrebouttierdev/hostdeck/internal/infrastructure/persistence/sqlite"
)

func TestSeedDemoDataCreatesFleet(t *testing.T) {
	db, err := sqlite.Open(":memory:")
	if err != nil {
		t.Fatal(err)
	}
	defer db.Close()
	ctx := context.Background()
	if err := db.Migrate(ctx); err != nil {
		t.Fatal(err)
	}

	servers := sqlite.NewServerRepository(db)
	metrics := sqlite.NewMetricsRepository(db)
	incidents := sqlite.NewIncidentRepository(db)
	alerts := sqlite.NewAlertRuleRepository(db)
	settings := sqlite.NewSettingsRepository(db)
	dockerRT := dockerruntime.NewDemoRuntime()

	if err := demo.SeedDemoData(ctx, demo.Repos{
		Servers: servers, Metrics: metrics, Incidents: incidents,
		Alerts: alerts, Settings: settings, Docker: dockerRT,
	}); err != nil {
		t.Fatal(err)
	}

	list, err := servers.List(ctx)
	if err != nil {
		t.Fatal(err)
	}
	if len(list) < 12 {
		t.Fatalf("expected >= 12 servers, got %d", len(list))
	}

	names := map[string]bool{}
	for _, srv := range list {
		names[srv.Name] = true
	}
	for _, want := range []string{"web-front-01", "db-core-01", "api-prod-01", "monitor-01", "bastion-01"} {
		if !names[want] {
			t.Fatalf("missing server %s", want)
		}
	}

	sample, err := metrics.Latest(ctx, list[0].ID)
	if err != nil {
		t.Fatalf("latest metrics: %v", err)
	}
	if sample.CPUTotalPercent <= 0 {
		t.Fatal("expected non-zero CPU sample")
	}

	incs, err := incidents.List(ctx, true)
	if err != nil {
		t.Fatal(err)
	}
	if len(incs) == 0 {
		t.Fatal("expected seeded incidents")
	}

	rules, err := alerts.List(ctx)
	if err != nil {
		t.Fatal(err)
	}
	if len(rules) == 0 {
		t.Fatal("expected seeded alert rules")
	}

	cfg, err := settings.Get(ctx)
	if err != nil {
		t.Fatal(err)
	}
	if cfg.InstanceName == "" {
		t.Fatal("expected settings instance name")
	}

	// Idempotent second seed.
	if err := demo.SeedDemoData(ctx, demo.Repos{Servers: servers, Metrics: metrics}); err != nil {
		t.Fatal(err)
	}
	list2, err := servers.List(ctx)
	if err != nil {
		t.Fatal(err)
	}
	if len(list2) != len(list) {
		t.Fatalf("idempotent seed changed count %d -> %d", len(list), len(list2))
	}
}
