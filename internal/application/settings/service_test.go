package settings_test

import (
	"context"
	"testing"
	"time"

	"github.com/alexandrebouttierdev/hostdeck/internal/application/settings"
	"github.com/alexandrebouttierdev/hostdeck/internal/infrastructure/persistence/sqlite"
)

type fixedClock struct{ t time.Time }

func (c fixedClock) Now() time.Time { return c.t }

func TestSettingsGetUpdate(t *testing.T) {
	db, err := sqlite.Open(":memory:")
	if err != nil {
		t.Fatal(err)
	}
	defer db.Close()
	ctx := context.Background()
	if err := db.Migrate(ctx); err != nil {
		t.Fatal(err)
	}
	now := time.Date(2026, 9, 6, 14, 0, 0, 0, time.UTC)
	svc := settings.NewService(sqlite.NewSettingsRepository(db), fixedClock{t: now})

	got, err := svc.Get(ctx)
	if err != nil {
		t.Fatal(err)
	}
	got.InstanceName = "lab"
	got.MetricsRetentionDays = 45
	got.NotifyDesktop = true
	updated, err := svc.Update(ctx, got)
	if err != nil {
		t.Fatal(err)
	}
	if updated.InstanceName != "lab" || updated.MetricsRetentionDays != 45 || !updated.NotifyDesktop {
		t.Fatalf("unexpected update: %+v", updated)
	}
	if !updated.UpdatedAt.Equal(now) {
		t.Fatalf("UpdatedAt=%v want %v", updated.UpdatedAt, now)
	}
	again, err := svc.Get(ctx)
	if err != nil {
		t.Fatal(err)
	}
	if again.InstanceName != "lab" {
		t.Fatalf("persisted name=%s", again.InstanceName)
	}
}
