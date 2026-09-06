package alerts_test

import (
	"context"
	"errors"
	"testing"
	"time"

	"github.com/alexandrebouttierdev/hostdeck/internal/application/alerts"
	"github.com/alexandrebouttierdev/hostdeck/internal/application/dto"
	"github.com/alexandrebouttierdev/hostdeck/internal/domain/shared"
	"github.com/alexandrebouttierdev/hostdeck/internal/infrastructure/persistence/sqlite"
)

type fixedClock struct{ t time.Time }

func (c fixedClock) Now() time.Time { return c.t }

func TestAlertCRUD(t *testing.T) {
	db, err := sqlite.Open(":memory:")
	if err != nil {
		t.Fatal(err)
	}
	defer db.Close()
	ctx := context.Background()
	if err := db.Migrate(ctx); err != nil {
		t.Fatal(err)
	}

	now := time.Date(2026, 9, 6, 12, 0, 0, 0, time.UTC)
	svc := alerts.NewService(sqlite.NewAlertRuleRepository(db), fixedClock{t: now})

	created, err := svc.Create(ctx, dto.AlertRuleDTO{
		Name: "CPU high", Metric: "cpu", Operator: ">", Threshold: 90, DurationSeconds: 60, Severity: "warning", Enabled: true,
	})
	if err != nil {
		t.Fatal(err)
	}
	if created.ID == "" {
		t.Fatal("expected id")
	}
	list, err := svc.List(ctx)
	if err != nil || len(list) != 1 {
		t.Fatalf("list=%v err=%v", list, err)
	}
	created.Threshold = 95
	if _, err := svc.Update(ctx, created); err != nil {
		t.Fatal(err)
	}
	got, err := svc.Get(ctx, created.ID)
	if err != nil || got.Threshold != 95 {
		t.Fatalf("got=%v err=%v", got, err)
	}
	if err := svc.Delete(ctx, created.ID); err != nil {
		t.Fatal(err)
	}
	_, err = svc.Get(ctx, created.ID)
	if !errors.Is(err, shared.ErrNotFound) {
		t.Fatalf("want not found, got %v", err)
	}
}
