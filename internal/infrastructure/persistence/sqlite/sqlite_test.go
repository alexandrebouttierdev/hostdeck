package sqlite_test

import (
	"context"
	"errors"
	"path/filepath"
	"testing"
	"time"

	"github.com/alexandrebouttierdev/hostdeck/internal/application/ports"
	"github.com/alexandrebouttierdev/hostdeck/internal/domain/incident"
	"github.com/alexandrebouttierdev/hostdeck/internal/domain/monitoring"
	"github.com/alexandrebouttierdev/hostdeck/internal/domain/server"
	"github.com/alexandrebouttierdev/hostdeck/internal/domain/shared"
	"github.com/alexandrebouttierdev/hostdeck/internal/infrastructure/persistence/migrate"
	"github.com/alexandrebouttierdev/hostdeck/internal/infrastructure/persistence/sqlite"
)

func openTestDB(t *testing.T) *sqlite.DB {
	t.Helper()
	path := filepath.Join(t.TempDir(), "hostdeck.db")
	db, err := sqlite.Open(path)
	if err != nil {
		t.Fatalf("open: %v", err)
	}
	t.Cleanup(func() { _ = db.Close() })
	if err := db.Migrate(context.Background()); err != nil {
		t.Fatalf("migrate: %v", err)
	}
	return db
}

func openMemoryDB(t *testing.T) *sqlite.DB {
	t.Helper()
	db, err := sqlite.Open(":memory:")
	if err != nil {
		t.Fatalf("open memory: %v", err)
	}
	t.Cleanup(func() { _ = db.Close() })
	if err := db.Migrate(context.Background()); err != nil {
		t.Fatalf("migrate: %v", err)
	}
	return db
}

func TestMigrate_CreatesSchemaAndIsIdempotent(t *testing.T) {
	db := openTestDB(t)
	ctx := context.Background()

	version, err := migrate.CurrentVersion(ctx, db.SQL())
	if err != nil {
		t.Fatalf("current version: %v", err)
	}
	if version != 1 {
		t.Fatalf("version = %d, want 1", version)
	}

	if err := db.Migrate(ctx); err != nil {
		t.Fatalf("second migrate: %v", err)
	}

	tables := []string{
		"servers", "server_tags", "metric_samples", "incidents",
		"alert_rules", "settings", "ssh_host_keys", "schema_migrations",
	}
	for _, table := range tables {
		var name string
		err := db.SQL().QueryRowContext(ctx,
			`SELECT name FROM sqlite_master WHERE type='table' AND name=?`, table).Scan(&name)
		if err != nil {
			t.Fatalf("table %s missing: %v", table, err)
		}
	}

	var fk int
	if err := db.SQL().QueryRow(`PRAGMA foreign_keys`).Scan(&fk); err != nil {
		t.Fatalf("pragma foreign_keys: %v", err)
	}
	if fk != 1 {
		t.Fatalf("foreign_keys = %d, want 1", fk)
	}
}

func TestMigrate_Memory(t *testing.T) {
	_ = openMemoryDB(t)
}

func sampleServer(name string) server.Server {
	now := time.Now().UTC().Truncate(time.Millisecond)
	return server.Server{
		ID:                server.NewServerID(),
		Name:              name,
		Host:              "10.0.0.1",
		Port:              22,
		Username:          "root",
		ConnectionMode:    server.ConnectionModeDirect,
		AuthMethod:        server.AuthMethodKey,
		CredentialRef:     "cred-1",
		Group:             "prod",
		Environment:       "production",
		Role:              "web",
		OSFamily:          "linux",
		OSName:            "ubuntu",
		Tags:              []string{"edge", "web"},
		Status:            server.ServerStatusOnline,
		MonitoringEnabled: true,
		DockerEnabled:     true,
		IntervalSeconds:   30,
		CreatedAt:         now,
		UpdatedAt:         now,
	}
}

func TestServerRepository_CRUD(t *testing.T) {
	db := openTestDB(t)
	ctx := context.Background()
	repo := sqlite.NewServerRepository(db)

	var _ ports.ServerRepository = repo

	srv := sampleServer("web-01")
	if err := repo.Create(ctx, srv); err != nil {
		t.Fatalf("create: %v", err)
	}

	got, err := repo.Get(ctx, srv.ID)
	if err != nil {
		t.Fatalf("get: %v", err)
	}
	if got.Name != srv.Name || got.Host != srv.Host || got.Port != srv.Port {
		t.Fatalf("got %+v", got)
	}
	if len(got.Tags) != 2 || got.Tags[0] != "edge" || got.Tags[1] != "web" {
		t.Fatalf("tags = %v", got.Tags)
	}
	if !got.MonitoringEnabled || !got.DockerEnabled {
		t.Fatalf("flags not persisted")
	}

	got.Name = "web-01-renamed"
	got.Tags = []string{"api"}
	got.Status = server.ServerStatusWarning
	got.UpdatedAt = time.Now().UTC()
	if err := repo.Update(ctx, got); err != nil {
		t.Fatalf("update: %v", err)
	}

	got2, err := repo.Get(ctx, srv.ID)
	if err != nil {
		t.Fatalf("get after update: %v", err)
	}
	if got2.Name != "web-01-renamed" || got2.Status != server.ServerStatusWarning {
		t.Fatalf("update not applied: %+v", got2)
	}
	if len(got2.Tags) != 1 || got2.Tags[0] != "api" {
		t.Fatalf("tags after update = %v", got2.Tags)
	}

	list, err := repo.List(ctx)
	if err != nil {
		t.Fatalf("list: %v", err)
	}
	if len(list) != 1 {
		t.Fatalf("list len = %d", len(list))
	}

	if err := repo.Delete(ctx, srv.ID); err != nil {
		t.Fatalf("delete: %v", err)
	}
	_, err = repo.Get(ctx, srv.ID)
	if !errors.Is(err, shared.ErrNotFound) {
		t.Fatalf("expected ErrNotFound, got %v", err)
	}
}

func TestMetricsRepository_HistoryAndPrune(t *testing.T) {
	db := openTestDB(t)
	ctx := context.Background()
	servers := sqlite.NewServerRepository(db)
	metrics := sqlite.NewMetricsRepository(db)

	var _ ports.MetricsRepository = metrics

	srv := sampleServer("metrics-host")
	if err := servers.Create(ctx, srv); err != nil {
		t.Fatalf("create server: %v", err)
	}

	base := time.Date(2026, 9, 6, 10, 0, 0, 0, time.UTC)
	for i := 0; i < 5; i++ {
		sample := monitoring.MetricSample{
			ID:              shared.NewID(),
			ServerID:        srv.ID,
			CollectedAt:     base.Add(time.Duration(i) * time.Minute),
			CPUTotalPercent: float64(10 + i),
			MemoryUsedBytes: uint64(1000 * (i + 1)),
			MemoryTotalBytes: 8000,
			Load1:           float64(i),
			DiskUsedBytes:   500,
			DiskTotalBytes:  1000,
			UptimeSeconds:   uint64(3600 + i),
		}
		if err := metrics.SaveSample(ctx, sample); err != nil {
			t.Fatalf("save sample %d: %v", i, err)
		}
	}

	latest, err := metrics.Latest(ctx, srv.ID)
	if err != nil {
		t.Fatalf("latest: %v", err)
	}
	if latest.CPUTotalPercent != 14 {
		t.Fatalf("latest cpu = %v, want 14", latest.CPUTotalPercent)
	}

	hist, err := metrics.History(ctx, srv.ID, base, base.Add(10*time.Minute), 3)
	if err != nil {
		t.Fatalf("history: %v", err)
	}
	if len(hist) != 3 {
		t.Fatalf("history len = %d, want 3", len(hist))
	}
	if hist[0].CPUTotalPercent != 10 || hist[2].CPUTotalPercent != 12 {
		t.Fatalf("history order/values = %+v", hist)
	}

	n, err := metrics.PruneBefore(ctx, base.Add(2*time.Minute))
	if err != nil {
		t.Fatalf("prune: %v", err)
	}
	if n != 2 {
		t.Fatalf("pruned = %d, want 2", n)
	}
}

func TestIncidentRepository_AcknowledgeFlow(t *testing.T) {
	db := openTestDB(t)
	ctx := context.Background()
	servers := sqlite.NewServerRepository(db)
	incidents := sqlite.NewIncidentRepository(db)

	var _ ports.IncidentRepository = incidents

	srv := sampleServer("inc-host")
	if err := servers.Create(ctx, srv); err != nil {
		t.Fatalf("create server: %v", err)
	}

	now := time.Now().UTC().Truncate(time.Millisecond)
	inc := incident.Incident{
		ID:            incident.NewIncidentID(),
		DisplayID:     "INC-1001",
		ServerID:      srv.ID,
		ServerName:    srv.Name,
		Metric:        "cpu",
		Problem:       "CPU high",
		Severity:      incident.SeverityHigh,
		RuleID:        "rule-1",
		RuleName:      "CPU > 90",
		Status:        incident.IncidentStatusOpen,
		CurrentValue:  95,
		Threshold:     90,
		StartedAt:     now,
		LastUpdatedAt: now,
	}
	if err := incidents.Create(ctx, inc); err != nil {
		t.Fatalf("create: %v", err)
	}

	active, err := incidents.List(ctx, true)
	if err != nil || len(active) != 1 {
		t.Fatalf("active list = %v err=%v", active, err)
	}

	ack := now.Add(time.Minute)
	inc.Status = incident.IncidentStatusAcknowledged
	inc.AcknowledgedAt = &ack
	inc.AcknowledgedBy = "ops"
	inc.LastUpdatedAt = ack
	if err := incidents.Update(ctx, inc); err != nil {
		t.Fatalf("acknowledge: %v", err)
	}

	got, err := incidents.Get(ctx, inc.ID)
	if err != nil {
		t.Fatalf("get: %v", err)
	}
	if got.Status != incident.IncidentStatusAcknowledged {
		t.Fatalf("status = %s", got.Status)
	}
	if got.AcknowledgedBy != "ops" || got.AcknowledgedAt == nil {
		t.Fatalf("ack fields missing: %+v", got)
	}

	resolved := ack.Add(time.Minute)
	inc.Status = incident.IncidentStatusResolved
	inc.ResolvedAt = &resolved
	inc.LastUpdatedAt = resolved
	if err := incidents.Update(ctx, inc); err != nil {
		t.Fatalf("resolve: %v", err)
	}

	active, err = incidents.List(ctx, true)
	if err != nil {
		t.Fatalf("list active: %v", err)
	}
	if len(active) != 0 {
		t.Fatalf("expected no active incidents, got %d", len(active))
	}

	all, err := incidents.List(ctx, false)
	if err != nil || len(all) != 1 {
		t.Fatalf("all list = %v err=%v", all, err)
	}
}

func TestSettingsRepository_GetAndSave(t *testing.T) {
	db := openTestDB(t)
	ctx := context.Background()
	repo := sqlite.NewSettingsRepository(db)

	var _ ports.SettingsRepository = repo

	defaults, err := repo.Get(ctx)
	if err != nil {
		t.Fatalf("get defaults: %v", err)
	}
	if defaults.InstanceName == "" {
		t.Fatalf("expected default instance name, got empty")
	}

	settings := shared.DefaultSettings()
	settings.InstanceName = "HostDeck Lab"
	settings.Theme = "light"
	settings.NotifySlack = true
	settings.MetricsRetentionDays = 30
	settings.UpdatedAt = time.Now().UTC().Truncate(time.Millisecond)

	if err := repo.Save(ctx, settings); err != nil {
		t.Fatalf("save: %v", err)
	}

	got, err := repo.Get(ctx)
	if err != nil {
		t.Fatalf("get: %v", err)
	}
	if got.InstanceName != "HostDeck Lab" || got.Theme != "light" {
		t.Fatalf("got %+v", got)
	}
	if !got.NotifySlack || got.MetricsRetentionDays != 30 {
		t.Fatalf("flags/retention not saved: %+v", got)
	}

	settings.InstanceName = "HostDeck Prod"
	if err := repo.Save(ctx, settings); err != nil {
		t.Fatalf("upsert: %v", err)
	}
	got, err = repo.Get(ctx)
	if err != nil {
		t.Fatalf("get after upsert: %v", err)
	}
	if got.InstanceName != "HostDeck Prod" {
		t.Fatalf("upsert failed: %s", got.InstanceName)
	}
}

func TestAlertRuleRepository_CRUD(t *testing.T) {
	db := openTestDB(t)
	ctx := context.Background()
	repo := sqlite.NewAlertRuleRepository(db)

	var _ ports.AlertRuleRepository = repo

	now := time.Now().UTC().Truncate(time.Millisecond)
	rule := incident.AlertRule{
		ID:              shared.NewID(),
		Name:            "High CPU",
		Enabled:         true,
		Metric:          "cpu",
		Operator:        ">",
		Threshold:       90,
		DurationSeconds: 300,
		Severity:        incident.SeverityCritical,
		CooldownSeconds: 60,
		Scope:           "all",
		Category:        "system",
		Status:          "ok",
		CreatedAt:       now,
		UpdatedAt:       now,
	}
	if err := repo.Create(ctx, rule); err != nil {
		t.Fatalf("create: %v", err)
	}

	got, err := repo.Get(ctx, rule.ID)
	if err != nil {
		t.Fatalf("get: %v", err)
	}
	if got.Name != rule.Name || got.Threshold != 90 {
		t.Fatalf("got %+v", got)
	}

	got.Enabled = false
	got.Threshold = 85
	got.UpdatedAt = now.Add(time.Minute)
	if err := repo.Update(ctx, got); err != nil {
		t.Fatalf("update: %v", err)
	}

	list, err := repo.List(ctx)
	if err != nil || len(list) != 1 || list[0].Threshold != 85 || list[0].Enabled {
		t.Fatalf("list = %+v err=%v", list, err)
	}

	if err := repo.Delete(ctx, rule.ID); err != nil {
		t.Fatalf("delete: %v", err)
	}
	_, err = repo.Get(ctx, rule.ID)
	if !errors.Is(err, shared.ErrNotFound) {
		t.Fatalf("expected not found, got %v", err)
	}
}
