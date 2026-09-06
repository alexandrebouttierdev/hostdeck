// Package bootstrap wires HostDeck application services.
package bootstrap

import (
	"context"
	"fmt"
	"os"
	"path/filepath"

	"github.com/alexandrebouttierdev/hostdeck/internal/application/alerts"
	dockerapp "github.com/alexandrebouttierdev/hostdeck/internal/application/docker"
	"github.com/alexandrebouttierdev/hostdeck/internal/application/incidents"
	"github.com/alexandrebouttierdev/hostdeck/internal/application/monitoring"
	"github.com/alexandrebouttierdev/hostdeck/internal/application/overview"
	"github.com/alexandrebouttierdev/hostdeck/internal/application/ports"
	"github.com/alexandrebouttierdev/hostdeck/internal/application/servers"
	"github.com/alexandrebouttierdev/hostdeck/internal/application/settings"
	"github.com/alexandrebouttierdev/hostdeck/internal/infrastructure/credentials"
	"github.com/alexandrebouttierdev/hostdeck/internal/infrastructure/demo"
	dockerruntime "github.com/alexandrebouttierdev/hostdeck/internal/infrastructure/docker"
	"github.com/alexandrebouttierdev/hostdeck/internal/infrastructure/logging"
	"github.com/alexandrebouttierdev/hostdeck/internal/infrastructure/notifications"
	"github.com/alexandrebouttierdev/hostdeck/internal/infrastructure/persistence/sqlite"
	sshinfra "github.com/alexandrebouttierdev/hostdeck/internal/infrastructure/ssh"
)

// Options configures application bootstrap.
type Options struct {
	DBPath        string
	SeedDemo      bool
	UseKeyring    bool
	UseDemoDocker bool
	LogLevel      string
}

// AppServices holds wired use cases and shared infrastructure.
type AppServices struct {
	DB *sqlite.DB

	Servers    *servers.Service
	Monitoring *monitoring.Service
	Incidents  *incidents.Service
	Alerts     *alerts.Service
	Settings   *settings.Service
	Docker     *dockerapp.Service
	Overview   *overview.Service

	ServerRepo    ports.ServerRepository
	MetricsRepo   ports.MetricsRepository
	IncidentRepo  ports.IncidentRepository
	AlertRepo     ports.AlertRuleRepository
	SettingsRepo  ports.SettingsRepository
	Credentials   ports.CredentialStore
	SSHFactory    ports.SSHConnectionFactory
	Containers    ports.ContainerRuntime
	Notifications ports.DesktopNotificationService
	Logger        *logging.Logger
}

// DefaultDBPath returns ~/.local/share/hostdeck/hostdeck.db.
func DefaultDBPath() (string, error) {
	home, err := os.UserHomeDir()
	if err != nil {
		return "", err
	}
	dir := filepath.Join(home, ".local", "share", "hostdeck")
	return filepath.Join(dir, "hostdeck.db"), nil
}

// Open constructs AppServices: opens DB, migrates, seeds if empty, wires use cases.
func Open(ctx context.Context, opts Options) (*AppServices, error) {
	log := logging.New(opts.LogLevel)
	dbPath := opts.DBPath
	if dbPath == "" {
		var err error
		dbPath, err = DefaultDBPath()
		if err != nil {
			return nil, err
		}
	}
	if dbPath != ":memory:" {
		if err := os.MkdirAll(filepath.Dir(dbPath), 0o755); err != nil {
			return nil, fmt.Errorf("create data dir: %w", err)
		}
	}

	db, err := sqlite.Open(dbPath)
	if err != nil {
		return nil, err
	}
	if err := db.Migrate(ctx); err != nil {
		_ = db.Close()
		return nil, fmt.Errorf("migrate: %w", err)
	}

	serverRepo := sqlite.NewServerRepository(db)
	metricsRepo := sqlite.NewMetricsRepository(db)
	incidentRepo := sqlite.NewIncidentRepository(db)
	alertRepo := sqlite.NewAlertRuleRepository(db)
	settingsRepo := sqlite.NewSettingsRepository(db)
	hostKeyStore := sqlite.NewHostKeyStore(db)

	var creds ports.CredentialStore
	if opts.UseKeyring {
		creds = credentials.NewKeyringStore()
	} else {
		creds = credentials.NewMemoryStore()
	}

	sshFactory := sshinfra.NewFactory(
		serverRepo,
		creds,
		sshinfra.HostKeyStoreAdapter{
			GetFn: func(ctx context.Context, host string, port int, keyType string) (string, string, error) {
				rec, err := hostKeyStore.Get(ctx, host, port, keyType)
				if err != nil {
					return "", "", err
				}
				return rec.Fingerprint, rec.PublicKey, nil
			},
			StoreFn: func(ctx context.Context, host string, port int, keyType, fingerprint, publicKey string) error {
				return hostKeyStore.Upsert(ctx, sqlite.HostKeyRecord{
					Host:        host,
					Port:        port,
					KeyType:     keyType,
					Fingerprint: fingerprint,
					PublicKey:   publicKey,
				})
			},
		},
		sshinfra.FactoryConfig{},
	)

	demoRT := dockerruntime.NewDemoRuntime()
	var containers ports.ContainerRuntime = demoRT
	if !opts.UseDemoDocker {
		containers = dockerruntime.NewSSHDockerRuntime(sshFactory, serverRepo)
	}

	seed := opts.SeedDemo
	if seed {
		if err := demo.SeedDemoData(ctx, demo.Repos{
			Servers:   serverRepo,
			Metrics:   metricsRepo,
			Incidents: incidentRepo,
			Alerts:    alertRepo,
			Settings:  settingsRepo,
			Docker:    demoRT,
		}); err != nil {
			_ = db.Close()
			return nil, fmt.Errorf("seed demo: %w", err)
		}
	}

	clock := ports.SystemClock()
	notify := notifications.NewDesktopService(log)

	app := &AppServices{
		DB:            db,
		Servers:       servers.NewService(serverRepo, metricsRepo, creds, sshFactory, clock),
		Monitoring:    monitoring.NewService(metricsRepo, serverRepo, clock),
		Incidents:     incidents.NewService(incidentRepo, clock),
		Alerts:        alerts.NewService(alertRepo, clock),
		Settings:      settings.NewService(settingsRepo, clock),
		Docker:        dockerapp.NewService(containers),
		Overview:      overview.NewService(serverRepo, metricsRepo, incidentRepo, alertRepo),
		ServerRepo:    serverRepo,
		MetricsRepo:   metricsRepo,
		IncidentRepo:  incidentRepo,
		AlertRepo:     alertRepo,
		SettingsRepo:  settingsRepo,
		Credentials:   creds,
		SSHFactory:    sshFactory,
		Containers:    containers,
		Notifications: notify,
		Logger:        log,
	}
	return app, nil
}

// Close releases resources.
func (a *AppServices) Close() error {
	if a == nil {
		return nil
	}
	if a.Monitoring != nil {
		_ = a.Monitoring.StopFleet(context.Background())
	}
	if a.DB != nil {
		return a.DB.Close()
	}
	return nil
}
