package ports

import (
	"context"
	"time"

	dockerdomain "github.com/alexandrebouttierdev/hostdeck/internal/domain/docker"
	"github.com/alexandrebouttierdev/hostdeck/internal/domain/incident"
	"github.com/alexandrebouttierdev/hostdeck/internal/domain/monitoring"
	"github.com/alexandrebouttierdev/hostdeck/internal/domain/server"
	"github.com/alexandrebouttierdev/hostdeck/internal/domain/shared"
)

// CommandResult représente le résultat d'une commande SSH.
type CommandResult struct {
	Stdout   string
	Stderr   string
	ExitCode int
	Duration time.Duration
}

// SSHConnection abstrait une session SSH ouverte.
type SSHConnection interface {
	Execute(ctx context.Context, command string) (CommandResult, error)
	Close() error
}

// SSHConnectionFactory crée des connexions SSH (direct ou jump).
type SSHConnectionFactory interface {
	Connect(ctx context.Context, srv server.Server) (SSHConnection, error)
}

// ServerRepository persiste les serveurs.
type ServerRepository interface {
	List(ctx context.Context) ([]server.Server, error)
	Get(ctx context.Context, id server.ServerID) (server.Server, error)
	Create(ctx context.Context, srv server.Server) error
	Update(ctx context.Context, srv server.Server) error
	Delete(ctx context.Context, id server.ServerID) error
}

// MetricsRepository persiste et interroge les métriques.
type MetricsRepository interface {
	SaveSample(ctx context.Context, sample monitoring.MetricSample) error
	Latest(ctx context.Context, serverID server.ServerID) (monitoring.MetricSample, error)
	History(ctx context.Context, serverID server.ServerID, from, to time.Time, limit int) ([]monitoring.MetricSample, error)
	PruneBefore(ctx context.Context, before time.Time) (int64, error)
}

// IncidentRepository persiste les incidents.
type IncidentRepository interface {
	List(ctx context.Context, onlyActive bool) ([]incident.Incident, error)
	Get(ctx context.Context, id incident.IncidentID) (incident.Incident, error)
	Create(ctx context.Context, inc incident.Incident) error
	Update(ctx context.Context, inc incident.Incident) error
}

// AlertRuleRepository persiste les règles d'alerte.
type AlertRuleRepository interface {
	List(ctx context.Context) ([]incident.AlertRule, error)
	Get(ctx context.Context, id string) (incident.AlertRule, error)
	Create(ctx context.Context, rule incident.AlertRule) error
	Update(ctx context.Context, rule incident.AlertRule) error
	Delete(ctx context.Context, id string) error
}

// SettingsRepository persiste les paramètres globaux.
type SettingsRepository interface {
	Get(ctx context.Context) (shared.Settings, error)
	Save(ctx context.Context, settings shared.Settings) error
}

// CredentialStore stocke les secrets hors SQLite.
type CredentialStore interface {
	Set(ctx context.Context, ref string, secret []byte) error
	Get(ctx context.Context, ref string) ([]byte, error)
	Delete(ctx context.Context, ref string) error
}

// ContainerRuntime abstrait Docker/Moby.
type ContainerRuntime interface {
	Info(ctx context.Context, serverID server.ServerID) (dockerdomain.HostInfo, error)
	ListContainers(ctx context.Context, serverID server.ServerID) ([]dockerdomain.Container, error)
	GetContainer(ctx context.Context, serverID server.ServerID, id dockerdomain.ContainerID) (dockerdomain.Container, error)
	Start(ctx context.Context, serverID server.ServerID, id dockerdomain.ContainerID) error
	Stop(ctx context.Context, serverID server.ServerID, id dockerdomain.ContainerID) error
	Restart(ctx context.Context, serverID server.ServerID, id dockerdomain.ContainerID) error
	Logs(ctx context.Context, serverID server.ServerID, id dockerdomain.ContainerID, tail int) ([]string, error)
}

// DesktopNotificationService envoie des notifications système.
type DesktopNotificationService interface {
	Notify(ctx context.Context, title, body string) error
}

// HostMetricCollector collects a MetricSample for one server (SSH, demo, etc.).
type HostMetricCollector interface {
	Collect(ctx context.Context, srv server.Server) (monitoring.MetricSample, error)
}

// Clock abstrait le temps pour les tests.
type Clock interface {
	Now() time.Time
}

type realClock struct{}

func (realClock) Now() time.Time { return time.Now().UTC() }

func SystemClock() Clock { return realClock{} }
