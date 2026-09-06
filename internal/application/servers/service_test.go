package servers_test

import (
	"context"
	"errors"
	"testing"
	"time"

	"github.com/alexandrebouttierdev/hostdeck/internal/application/dto"
	"github.com/alexandrebouttierdev/hostdeck/internal/application/ports"
	"github.com/alexandrebouttierdev/hostdeck/internal/application/servers"
	"github.com/alexandrebouttierdev/hostdeck/internal/domain/server"
	"github.com/alexandrebouttierdev/hostdeck/internal/domain/shared"
	"github.com/alexandrebouttierdev/hostdeck/internal/infrastructure/credentials"
	"github.com/alexandrebouttierdev/hostdeck/internal/infrastructure/persistence/sqlite"
)

type fixedClock struct{ t time.Time }

func (c fixedClock) Now() time.Time { return c.t }

func openTestDB(t *testing.T) (*sqlite.DB, *servers.Service) {
	t.Helper()
	db, err := sqlite.Open(":memory:")
	if err != nil {
		t.Fatal(err)
	}
	t.Cleanup(func() { _ = db.Close() })
	if err := db.Migrate(context.Background()); err != nil {
		t.Fatal(err)
	}
	svc := servers.NewService(
		sqlite.NewServerRepository(db),
		sqlite.NewMetricsRepository(db),
		credentials.NewMemoryStore(),
		nil,
		fixedClock{t: time.Date(2026, 9, 6, 10, 0, 0, 0, time.UTC)},
	)
	return db, svc
}

func TestAddServerValidation(t *testing.T) {
	_, svc := openTestDB(t)
	ctx := context.Background()

	_, err := svc.Add(ctx, dto.CreateServerDTO{
		Name: "", Host: "10.0.0.1", Port: 22, Username: "root",
		ConnectionMode: "direct", AuthMethod: "key",
	})
	var ve *shared.ValidationError
	if !errors.As(err, &ve) || ve.Field != "name" {
		t.Fatalf("expected name validation error, got %v", err)
	}

	_, err = svc.Add(ctx, dto.CreateServerDTO{
		Name: "web-1", Host: "", Port: 22, Username: "root",
		ConnectionMode: "direct", AuthMethod: "key",
	})
	if !errors.As(err, &ve) || ve.Field != "host" {
		t.Fatalf("expected host validation error, got %v", err)
	}

	_, err = svc.Add(ctx, dto.CreateServerDTO{
		Name: "web-1", Host: "10.0.0.1", Port: 99999, Username: "root",
		ConnectionMode: "direct", AuthMethod: "key",
	})
	if !errors.As(err, &ve) || ve.Field != "port" {
		t.Fatalf("expected port validation error, got %v", err)
	}

	got, err := svc.Add(ctx, dto.CreateServerDTO{
		Name: "  web-front-01  ", Host: "10.0.1.10", Port: 22, Username: "ubuntu",
		ConnectionMode: "direct", AuthMethod: "key",
		Tags: []string{"prod", "web"}, MonitoringEnabled: true,
		CredentialSecret: "secret-key-material",
	})
	if err != nil {
		t.Fatal(err)
	}
	if got.Name != "web-front-01" {
		t.Fatalf("name = %q", got.Name)
	}
	if got.ID == "" {
		t.Fatal("expected id")
	}
}

func TestJumpHostCycleDetection(t *testing.T) {
	_, svc := openTestDB(t)
	ctx := context.Background()

	a, err := svc.Add(ctx, dto.CreateServerDTO{
		Name: "bastion-01", Host: "10.0.0.5", Port: 22, Username: "ubuntu",
		ConnectionMode: "direct", AuthMethod: "key",
	})
	if err != nil {
		t.Fatal(err)
	}
	b, err := svc.Add(ctx, dto.CreateServerDTO{
		Name: "app-01", Host: "10.0.1.20", Port: 22, Username: "ubuntu",
		ConnectionMode: "jump", JumpHostID: a.ID, AuthMethod: "key",
	})
	if err != nil {
		t.Fatal(err)
	}

	// Create cycle: a jumps through b, while b jumps through a.
	_, err = svc.Update(ctx, dto.UpdateServerDTO{
		ID: a.ID, Name: a.Name, Host: a.Host, Port: 22, Username: a.Username,
		ConnectionMode: "jump", JumpHostID: b.ID, AuthMethod: "key",
	})
	var ve *shared.ValidationError
	if !errors.As(err, &ve) || ve.Field != "jump_host_id" {
		t.Fatalf("expected jump cycle validation, got %v", err)
	}

	// Missing jump host.
	_, err = svc.Add(ctx, dto.CreateServerDTO{
		Name: "orphan", Host: "10.0.9.9", Port: 22, Username: "ubuntu",
		ConnectionMode: "jump", JumpHostID: shared.NewID(), AuthMethod: "key",
	})
	if !errors.As(err, &ve) {
		t.Fatalf("expected validation error for missing jump host, got %v", err)
	}

	// Self jump.
	_, err = svc.Update(ctx, dto.UpdateServerDTO{
		ID: b.ID, Name: b.Name, Host: b.Host, Port: 22, Username: b.Username,
		ConnectionMode: "jump", JumpHostID: b.ID, AuthMethod: "key",
	})
	if !errors.As(err, &ve) {
		t.Fatalf("expected self-jump validation, got %v", err)
	}
}

func TestJumpHostHappyPath(t *testing.T) {
	db, svc := openTestDB(t)
	ctx := context.Background()
	_ = db

	bastion, err := svc.Add(ctx, dto.CreateServerDTO{
		Name: "bastion-01", Host: "10.0.0.5", Port: 22, Username: "ubuntu",
		ConnectionMode: string(server.ConnectionModeDirect), AuthMethod: "key",
	})
	if err != nil {
		t.Fatal(err)
	}
	target, err := svc.Add(ctx, dto.CreateServerDTO{
		Name: "api-prod-01", Host: "10.0.4.10", Port: 22, Username: "ubuntu",
		ConnectionMode: string(server.ConnectionModeJump), JumpHostID: bastion.ID, AuthMethod: "key",
	})
	if err != nil {
		t.Fatal(err)
	}
	if target.JumpHostID != bastion.ID {
		t.Fatalf("jump host = %q want %q", target.JumpHostID, bastion.ID)
	}
}

var _ ports.Clock = fixedClock{}
