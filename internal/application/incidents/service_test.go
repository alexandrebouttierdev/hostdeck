package incidents_test

import (
	"context"
	"errors"
	"testing"
	"time"

	"github.com/alexandrebouttierdev/hostdeck/internal/application/dto"
	"github.com/alexandrebouttierdev/hostdeck/internal/application/incidents"
	"github.com/alexandrebouttierdev/hostdeck/internal/domain/incident"
	"github.com/alexandrebouttierdev/hostdeck/internal/domain/server"
	"github.com/alexandrebouttierdev/hostdeck/internal/domain/shared"
	"github.com/alexandrebouttierdev/hostdeck/internal/infrastructure/persistence/sqlite"
)

type fixedClock struct{ t time.Time }

func (c fixedClock) Now() time.Time { return c.t }

func TestAcknowledgeIncident(t *testing.T) {
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
	incRepo := sqlite.NewIncidentRepository(db)
	now := time.Date(2026, 9, 6, 10, 24, 0, 0, time.UTC)
	svc := incidents.NewService(incRepo, fixedClock{t: now})

	srv := server.Server{
		ID: server.NewServerID(), Name: "monitor-01", Host: "10.0.8.10", Port: 22,
		Username: "ubuntu", ConnectionMode: server.ConnectionModeDirect, AuthMethod: server.AuthMethodKey,
		Status: server.ServerStatusWarning, MonitoringEnabled: true, IntervalSeconds: 60,
		CreatedAt: now, UpdatedAt: now,
	}
	if err := servers.Create(ctx, srv); err != nil {
		t.Fatal(err)
	}

	inc := incident.Incident{
		ID: incident.NewIncidentID(), DisplayID: "INC-8421",
		ServerID: srv.ID, ServerName: srv.Name,
		Metric: "load5", Problem: "Charge système élevée > 1.5",
		Severity: incident.SeverityCritical, Status: incident.IncidentStatusOpen,
		CurrentValue: 2.34, Threshold: 1.5,
		StartedAt: now.Add(-4 * time.Hour), LastUpdatedAt: now.Add(-time.Minute),
	}
	if err := incRepo.Create(ctx, inc); err != nil {
		t.Fatal(err)
	}

	_, err = svc.Acknowledge(ctx, dto.AcknowledgeIncidentDTO{IncidentID: string(inc.ID), By: ""})
	var ve *shared.ValidationError
	if !errors.As(err, &ve) || ve.Field != "by" {
		t.Fatalf("expected by validation, got %v", err)
	}

	got, err := svc.Acknowledge(ctx, dto.AcknowledgeIncidentDTO{
		IncidentID: string(inc.ID), By: "ops-team", Notes: "investigating",
	})
	if err != nil {
		t.Fatal(err)
	}
	if got.Status != string(incident.IncidentStatusAcknowledged) {
		t.Fatalf("status = %s", got.Status)
	}
	if got.AcknowledgedBy != "ops-team" {
		t.Fatalf("ack by = %s", got.AcknowledgedBy)
	}
	if got.AcknowledgedAt == nil || !got.AcknowledgedAt.Equal(now) {
		t.Fatalf("ack at = %v want %v", got.AcknowledgedAt, now)
	}
	if got.Notes != "investigating" {
		t.Fatalf("notes = %q", got.Notes)
	}

	resolved, err := svc.Resolve(ctx, dto.ResolveIncidentDTO{IncidentID: string(inc.ID), Notes: "fixed"})
	if err != nil {
		t.Fatal(err)
	}
	if resolved.Status != string(incident.IncidentStatusResolved) {
		t.Fatalf("status = %s", resolved.Status)
	}

	_, err = svc.Acknowledge(ctx, dto.AcknowledgeIncidentDTO{IncidentID: string(inc.ID), By: "ops"})
	if !errors.As(err, &ve) {
		t.Fatalf("expected cannot acknowledge closed incident, got %v", err)
	}
}
