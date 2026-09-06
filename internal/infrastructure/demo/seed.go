// Package demo seeds realistic HostDeck demo data for empty databases.
package demo

import (
	"context"
	"fmt"
	"math"
	"time"

	"github.com/alexandrebouttierdev/hostdeck/internal/application/ports"
	dockerdomain "github.com/alexandrebouttierdev/hostdeck/internal/domain/docker"
	"github.com/alexandrebouttierdev/hostdeck/internal/domain/incident"
	"github.com/alexandrebouttierdev/hostdeck/internal/domain/monitoring"
	"github.com/alexandrebouttierdev/hostdeck/internal/domain/server"
	"github.com/alexandrebouttierdev/hostdeck/internal/domain/shared"
	dockerruntime "github.com/alexandrebouttierdev/hostdeck/internal/infrastructure/docker"
)

// Repos groups persistence ports required for seeding.
type Repos struct {
	Servers   ports.ServerRepository
	Metrics   ports.MetricsRepository
	Incidents ports.IncidentRepository
	Alerts    ports.AlertRuleRepository
	Settings  ports.SettingsRepository
	Docker    *dockerruntime.DemoRuntime // optional
}

type seedServer struct {
	Name        string
	Host        string
	Port        int
	OSFamily    string
	OSName      string
	Group       string
	Environment string
	Role        string
	Tags        []string
	Status      server.ServerStatus
	Docker      bool
	CPU         float64
	Mem         float64
	Disk        float64
	Load1       float64
	RX          float64
	TX          float64
	UptimeDays  int
}

// SeedDemoData creates ~20 realistic servers, metric history, incidents, alert rules,
// and settings when the database has no servers yet. Idempotent if servers already exist.
func SeedDemoData(ctx context.Context, repos Repos) error {
	if repos.Servers == nil {
		return fmt.Errorf("demo seed: servers repository required")
	}
	existing, err := repos.Servers.List(ctx)
	if err != nil {
		return err
	}
	if len(existing) > 0 {
		return nil
	}

	now := time.Now().UTC()
	if repos.Settings != nil {
		cfg := shared.DefaultSettings()
		cfg.InstanceName = "HostDeck Production"
		cfg.Description = "Supervision de l'infrastructure de production"
		cfg.UpdatedAt = now
		if err := repos.Settings.Save(ctx, cfg); err != nil {
			return fmt.Errorf("seed settings: %w", err)
		}
	}

	defs := demoServerDefs()
	created := make([]server.Server, 0, len(defs))
	for i, def := range defs {
		srv := server.Server{
			ID:                server.NewServerID(),
			Name:              def.Name,
			Host:              def.Host,
			Port:              def.Port,
			Username:          "hostdeck",
			ConnectionMode:    server.ConnectionModeDirect,
			AuthMethod:        server.AuthMethodKey,
			CredentialRef:     "",
			Group:             def.Group,
			Environment:       def.Environment,
			Role:              def.Role,
			OSFamily:          def.OSFamily,
			OSName:            def.OSName,
			Tags:              def.Tags,
			Status:            def.Status,
			MonitoringEnabled: true,
			DockerEnabled:     def.Docker,
			IntervalSeconds:   60,
			CreatedAt:         now.Add(-time.Duration(30-i) * 24 * time.Hour),
			UpdatedAt:         now,
		}
		lc := now.Add(-time.Duration(15+i) * time.Second)
		srv.LastCollectedAt = &lc
		if err := repos.Servers.Create(ctx, srv); err != nil {
			return fmt.Errorf("seed server %s: %w", def.Name, err)
		}
		created = append(created, srv)

		if repos.Metrics != nil {
			if err := seedMetrics(ctx, repos.Metrics, srv.ID, def, now); err != nil {
				return err
			}
		}
		if repos.Docker != nil && def.Docker {
			seedDocker(repos.Docker, srv.ID, def.Name)
		}
	}

	byName := map[string]server.Server{}
	for _, srv := range created {
		byName[srv.Name] = srv
	}

	if repos.Alerts != nil {
		if err := seedAlerts(ctx, repos.Alerts, now); err != nil {
			return err
		}
	}
	if repos.Incidents != nil {
		if err := seedIncidents(ctx, repos.Incidents, byName, now); err != nil {
			return err
		}
	}
	return nil
}

func demoServerDefs() []seedServer {
	return []seedServer{
		{Name: "web-front-01", Host: "10.0.1.10", Port: 22, OSFamily: "linux", OSName: "Ubuntu 22.04 LTS", Group: "web", Environment: "production", Role: "frontend", Tags: []string{"prod", "web"}, Status: server.ServerStatusOnline, Docker: true, CPU: 22, Mem: 48, Disk: 41, Load1: 0.42, RX: 6e6, TX: 1.5e6, UptimeDays: 24},
		{Name: "web-front-02", Host: "10.0.1.11", Port: 22, OSFamily: "linux", OSName: "Ubuntu 22.04 LTS", Group: "web", Environment: "production", Role: "frontend", Tags: []string{"prod", "web"}, Status: server.ServerStatusOnline, Docker: true, CPU: 28, Mem: 52, Disk: 44, Load1: 0.61, RX: 5.2e6, TX: 1.2e6, UptimeDays: 18},
		{Name: "db-core-01", Host: "10.0.2.10", Port: 22, OSFamily: "linux", OSName: "PostgreSQL 15 (Linux)", Group: "database", Environment: "production", Role: "primary", Tags: []string{"prod", "db", "critique"}, Status: server.ServerStatusOffline, Docker: false, CPU: 92, Mem: 78, Disk: 85, Load1: 4.32, RX: 2.1e6, TX: 3.4e6, UptimeDays: 42},
		{Name: "db-core-02", Host: "10.0.2.11", Port: 22, OSFamily: "linux", OSName: "PostgreSQL 15 (Linux)", Group: "database", Environment: "production", Role: "replica", Tags: []string{"prod", "db"}, Status: server.ServerStatusWarning, Docker: false, CPU: 71, Mem: 74, Disk: 82, Load1: 2.85, RX: 1.8e6, TX: 2.9e6, UptimeDays: 40},
		{Name: "cache-01", Host: "10.0.3.10", Port: 22, OSFamily: "linux", OSName: "Redis 7 (Linux)", Group: "cache", Environment: "production", Role: "cache", Tags: []string{"prod", "cache"}, Status: server.ServerStatusOnline, Docker: true, CPU: 18, Mem: 61, Disk: 22, Load1: 0.31, RX: 8e6, TX: 7e6, UptimeDays: 60},
		{Name: "cache-02", Host: "10.0.3.11", Port: 22, OSFamily: "linux", OSName: "Redis 7 (Linux)", Group: "cache", Environment: "production", Role: "cache", Tags: []string{"prod", "cache"}, Status: server.ServerStatusOnline, Docker: true, CPU: 15, Mem: 55, Disk: 19, Load1: 0.27, RX: 7.2e6, TX: 6.5e6, UptimeDays: 55},
		{Name: "api-prod-01", Host: "10.0.4.10", Port: 22, OSFamily: "linux", OSName: "Rocky Linux 9", Group: "api", Environment: "production", Role: "api", Tags: []string{"prod", "api"}, Status: server.ServerStatusWarning, Docker: true, CPU: 73, Mem: 68, Disk: 51, Load1: 2.14, RX: 4.5e6, TX: 3.1e6, UptimeDays: 21},
		{Name: "api-prod-02", Host: "10.0.4.11", Port: 22, OSFamily: "linux", OSName: "Rocky Linux 9", Group: "api", Environment: "production", Role: "api", Tags: []string{"prod", "api"}, Status: server.ServerStatusOnline, Docker: true, CPU: 41, Mem: 58, Disk: 47, Load1: 1.02, RX: 3.8e6, TX: 2.6e6, UptimeDays: 21},
		{Name: "worker-01", Host: "10.0.5.10", Port: 22, OSFamily: "linux", OSName: "Debian 12", Group: "workers", Environment: "production", Role: "worker", Tags: []string{"prod", "worker"}, Status: server.ServerStatusOnline, Docker: true, CPU: 55, Mem: 49, Disk: 38, Load1: 1.45, RX: 1.2e6, TX: 0.9e6, UptimeDays: 12},
		{Name: "worker-02", Host: "10.0.5.11", Port: 22, OSFamily: "linux", OSName: "Debian 12", Group: "workers", Environment: "production", Role: "worker", Tags: []string{"prod", "worker"}, Status: server.ServerStatusOnline, Docker: true, CPU: 48, Mem: 46, Disk: 36, Load1: 1.18, RX: 1.0e6, TX: 0.8e6, UptimeDays: 12},
		{Name: "storage-01", Host: "10.0.6.10", Port: 22, OSFamily: "linux", OSName: "Ubuntu 22.04 LTS", Group: "storage", Environment: "production", Role: "nfs", Tags: []string{"prod", "stockage"}, Status: server.ServerStatusOnline, Docker: false, CPU: 12, Mem: 34, Disk: 72, Load1: 0.55, RX: 12e6, TX: 9e6, UptimeDays: 90},
		{Name: "nfs-storage-01", Host: "10.0.6.20", Port: 22, OSFamily: "linux", OSName: "Ubuntu 22.04 LTS", Group: "storage", Environment: "production", Role: "nfs", Tags: []string{"prod", "stockage"}, Status: server.ServerStatusWarning, Docker: false, CPU: 19, Mem: 41, Disk: 74, Load1: 0.88, RX: 10e6, TX: 8e6, UptimeDays: 88},
		{Name: "backup-01", Host: "10.0.7.10", Port: 22, OSFamily: "linux", OSName: "Ubuntu 22.04 LTS", Group: "backup", Environment: "production", Role: "backup", Tags: []string{"backup", "critique"}, Status: server.ServerStatusOffline, Docker: false, CPU: 88, Mem: 62, Disk: 93, Load1: 3.21, RX: 15e6, TX: 2e6, UptimeDays: 7},
		{Name: "monitor-01", Host: "10.0.8.10", Port: 22, OSFamily: "linux", OSName: "Ubuntu 22.04 LTS", Group: "infra", Environment: "production", Role: "monitoring", Tags: []string{"monitoring", "infra"}, Status: server.ServerStatusWarning, Docker: true, CPU: 64, Mem: 71, Disk: 58, Load1: 2.34, RX: 2.2e6, TX: 1.1e6, UptimeDays: 33},
		{Name: "logs-01", Host: "10.0.8.11", Port: 22, OSFamily: "linux", OSName: "Debian 12", Group: "infra", Environment: "production", Role: "logging", Tags: []string{"logging", "infra"}, Status: server.ServerStatusOnline, Docker: true, CPU: 35, Mem: 67, Disk: 64, Load1: 0.95, RX: 4e6, TX: 0.5e6, UptimeDays: 45},
		{Name: "edge-par-01", Host: "10.0.11.10", Port: 22, OSFamily: "linux", OSName: "Ubuntu 22.04 LTS", Group: "edge", Environment: "production", Role: "edge", Tags: []string{"prod", "edge"}, Status: server.ServerStatusWarning, Docker: true, CPU: 81, Mem: 54, Disk: 39, Load1: 2.67, RX: 20e6, TX: 18e6, UptimeDays: 14},
		{Name: "analytics-01", Host: "10.0.12.10", Port: 22, OSFamily: "linux", OSName: "Rocky Linux 9", Group: "analytics", Environment: "production", Role: "analytics", Tags: []string{"prod", "analytics"}, Status: server.ServerStatusOnline, Docker: true, CPU: 57, Mem: 72, Disk: 55, Load1: 1.76, RX: 3e6, TX: 2.4e6, UptimeDays: 28},
		{Name: "git-01", Host: "10.0.9.10", Port: 22, OSFamily: "linux", OSName: "Ubuntu 22.04 LTS", Group: "dev", Environment: "development", Role: "vcs", Tags: []string{"dev", "tools"}, Status: server.ServerStatusWarning, Docker: true, CPU: 44, Mem: 58, Disk: 61, Load1: 1.33, RX: 1.5e6, TX: 1.2e6, UptimeDays: 50},
		{Name: "ci-runner-01", Host: "10.0.9.11", Port: 22, OSFamily: "linux", OSName: "Debian 12", Group: "dev", Environment: "development", Role: "ci", Tags: []string{"dev", "ci"}, Status: server.ServerStatusOnline, Docker: true, CPU: 62, Mem: 48, Disk: 43, Load1: 2.01, RX: 2.8e6, TX: 1.9e6, UptimeDays: 9},
		{Name: "bastion-01", Host: "10.0.0.5", Port: 22, OSFamily: "linux", OSName: "Ubuntu 22.04 LTS", Group: "infra", Environment: "production", Role: "bastion", Tags: []string{"infra", "bastion"}, Status: server.ServerStatusOnline, Docker: false, CPU: 8, Mem: 22, Disk: 18, Load1: 0.12, RX: 0.5e6, TX: 0.4e6, UptimeDays: 120},
	}
}

func seedMetrics(ctx context.Context, repo ports.MetricsRepository, id server.ServerID, def seedServer, now time.Time) error {
	memTotal := uint64(16 << 30)
	diskTotal := uint64(500 << 30)
	points := 36 // 6h at 10m
	for i := points; i >= 0; i-- {
		t := now.Add(-time.Duration(i) * 10 * time.Minute)
		wave := 1 + 0.15*math.Sin(float64(i)/3.0)
		cpu := clamp(def.CPU*wave, 1, 99)
		memPct := clamp(def.Mem*(0.95+0.05*math.Cos(float64(i)/4.0)), 5, 98)
		diskPct := clamp(def.Disk+float64(points-i)*0.02, 5, 98)
		sample := monitoring.MetricSample{
			ID:                   shared.NewID(),
			ServerID:             id,
			CollectedAt:          t,
			CPUUserPercent:       cpu * 0.65,
			CPUSystemPercent:     cpu * 0.2,
			CPUIOWaitPercent:     cpu * 0.1,
			CPUNicePercent:       cpu * 0.03,
			CPUStealPercent:      cpu * 0.02,
			CPUTotalPercent:      cpu,
			MemoryTotalBytes:     memTotal,
			MemoryUsedBytes:      uint64(memPct / 100 * float64(memTotal)),
			MemoryCacheBytes:     uint64(0.2 * float64(memTotal)),
			MemoryBufferBytes:    uint64(0.05 * float64(memTotal)),
			MemoryFreeBytes:      uint64((100 - memPct) / 100 * float64(memTotal) * 0.5),
			SwapTotalBytes:       2 << 30,
			SwapUsedBytes:        uint64(clamp(memPct-70, 0, 30) / 100 * float64(2<<30)),
			Load1:                def.Load1 * wave,
			Load5:                def.Load1 * 0.9 * wave,
			Load15:               def.Load1 * 0.75,
			NetworkRXBytesPerSec: def.RX * (0.8 + 0.2*math.Sin(float64(i))),
			NetworkTXBytesPerSec: def.TX * (0.8 + 0.2*math.Cos(float64(i))),
			DiskUsedBytes:        uint64(diskPct / 100 * float64(diskTotal)),
			DiskTotalBytes:       diskTotal,
			DiskReadBytesPerSec:  1.2e6,
			DiskWriteBytesPerSec: 0.8e6,
			UptimeSeconds:        uint64(def.UptimeDays*86400 + i*600),
		}
		if err := repo.SaveSample(ctx, sample); err != nil {
			return fmt.Errorf("seed metrics %s: %w", def.Name, err)
		}
	}
	return nil
}

func seedAlerts(ctx context.Context, repo ports.AlertRuleRepository, now time.Time) error {
	rules := []incident.AlertRule{
		{ID: shared.NewID(), Name: "CPU Load > 1.5 (5m)", Enabled: true, Metric: "load5", Operator: ">", Threshold: 1.5, DurationSeconds: 300, Severity: incident.SeverityCritical, CooldownSeconds: 600, Scope: "all", Category: "system", Expression: "load5 > 1.5", Status: "ok", CreatedAt: now, UpdatedAt: now},
		{ID: shared.NewID(), Name: "CPU élevée > 80%", Enabled: true, Metric: "cpu_total", Operator: ">", Threshold: 80, DurationSeconds: 300, Severity: incident.SeverityHigh, CooldownSeconds: 300, Scope: "all", Category: "system", Expression: "cpu_total > 80", Status: "triggered", CreatedAt: now, UpdatedAt: now},
		{ID: shared.NewID(), Name: "Mémoire utilisée > 90%", Enabled: true, Metric: "memory_used_percent", Operator: ">", Threshold: 90, DurationSeconds: 300, Severity: incident.SeverityHigh, CooldownSeconds: 300, Scope: "all", Category: "system", Expression: "memory > 90", Status: "ok", CreatedAt: now, UpdatedAt: now},
		{ID: shared.NewID(), Name: "Espace disque faible > 70%", Enabled: true, Metric: "disk_used_percent", Operator: ">", Threshold: 70, DurationSeconds: 600, Severity: incident.SeverityWarning, CooldownSeconds: 900, Scope: "all", Category: "storage", Expression: "disk > 70", Status: "triggered", CreatedAt: now, UpdatedAt: now},
		{ID: shared.NewID(), Name: "Utilisation disque /var > 85%", Enabled: true, Metric: "disk_used_percent", Operator: ">", Threshold: 85, DurationSeconds: 300, Severity: incident.SeverityCritical, CooldownSeconds: 600, Scope: "database", Category: "storage", Expression: "disk > 85", Status: "triggered", CreatedAt: now, UpdatedAt: now},
		{ID: shared.NewID(), Name: "Latence Redis > 10 ms", Enabled: true, Metric: "redis_latency_ms", Operator: ">", Threshold: 10, DurationSeconds: 180, Severity: incident.SeverityWarning, CooldownSeconds: 300, Scope: "cache", Category: "app", Expression: "redis_latency > 10", Status: "ok", CreatedAt: now, UpdatedAt: now},
		{ID: shared.NewID(), Name: "Hôte hors ligne", Enabled: true, Metric: "availability", Operator: "==", Threshold: 0, DurationSeconds: 120, Severity: incident.SeverityCritical, CooldownSeconds: 120, Scope: "all", Category: "availability", Expression: "online == 0", Status: "ok", CreatedAt: now, UpdatedAt: now},
		{ID: shared.NewID(), Name: "Erreurs HTTP 5xx > 2%", Enabled: true, Metric: "http_5xx_rate", Operator: ">", Threshold: 2, DurationSeconds: 180, Severity: incident.SeverityHigh, CooldownSeconds: 300, Scope: "web", Category: "app", Expression: "http_5xx > 2", Status: "triggered", CreatedAt: now, UpdatedAt: now},
	}
	for _, rule := range rules {
		if err := repo.Create(ctx, rule); err != nil {
			return fmt.Errorf("seed alert %s: %w", rule.Name, err)
		}
	}
	return nil
}

func seedIncidents(ctx context.Context, repo ports.IncidentRepository, byName map[string]server.Server, now time.Time) error {
	type def struct {
		DisplayID string
		Host      string
		Metric    string
		Problem   string
		Severity  incident.Severity
		Status    incident.IncidentStatus
		Value     float64
		Threshold float64
		Age       time.Duration
		AckBy     string
		RuleName  string
	}
	defs := []def{
		{DisplayID: "INC-8421", Host: "monitor-01", Metric: "load5", Problem: "Charge système élevée > 1.5 (moy. 5m)", Severity: incident.SeverityCritical, Status: incident.IncidentStatusOpen, Value: 2.34, Threshold: 1.5, Age: 3*time.Hour + 57*time.Minute, RuleName: "CPU Load > 1.5 (5m)"},
		{DisplayID: "INC-8418", Host: "db-core-02", Metric: "disk_used_percent", Problem: "Utilisation disque élevée sur /var", Severity: incident.SeverityCritical, Status: incident.IncidentStatusOpen, Value: 87, Threshold: 85, Age: 4*time.Hour + 11*time.Minute, RuleName: "Utilisation disque /var > 85%"},
		{DisplayID: "INC-8415", Host: "edge-par-01", Metric: "cpu_total", Problem: "CPU élevée > 80%", Severity: incident.SeverityHigh, Status: incident.IncidentStatusOpen, Value: 86, Threshold: 80, Age: 2*time.Hour + 45*time.Minute, RuleName: "CPU élevée > 80%"},
		{DisplayID: "INC-8412", Host: "web-front-02", Metric: "http_5xx_rate", Problem: "Taux d'erreurs HTTP 5xx > 2%", Severity: incident.SeverityHigh, Status: incident.IncidentStatusAcknowledged, Value: 3.4, Threshold: 2, Age: 46 * time.Minute, AckBy: "ops-team", RuleName: "Erreurs HTTP 5xx > 2%"},
		{DisplayID: "INC-8409", Host: "nfs-storage-01", Metric: "disk_used_percent", Problem: "Espace disque faible > 70%", Severity: incident.SeverityWarning, Status: incident.IncidentStatusOpen, Value: 74, Threshold: 70, Age: 2*time.Hour + 9*time.Minute, RuleName: "Espace disque faible > 70%"},
		{DisplayID: "INC-8407", Host: "cache-01", Metric: "redis_latency_ms", Problem: "Latence Redis > 10 ms", Severity: incident.SeverityWarning, Status: incident.IncidentStatusOpen, Value: 14, Threshold: 10, Age: 1*time.Hour + 28*time.Minute, RuleName: "Latence Redis > 10 ms"},
		{DisplayID: "INC-8404", Host: "api-prod-01", Metric: "auth_failures", Problem: "Échecs d'authentification > 100/min", Severity: incident.SeverityCritical, Status: incident.IncidentStatusAcknowledged, Value: 142, Threshold: 100, Age: 1*time.Hour + 16*time.Minute, AckBy: "secops", RuleName: "Auth failures"},
		{DisplayID: "INC-8401", Host: "worker-01", Metric: "queue_depth", Problem: "File de travail > 1000 éléments", Severity: incident.SeverityWarning, Status: incident.IncidentStatusOpen, Value: 1280, Threshold: 1000, Age: 5*time.Hour + 26*time.Minute, RuleName: "Queue depth"},
		{DisplayID: "INC-8398", Host: "backup-01", Metric: "backup_lag", Problem: "Sauvegarde en retard", Severity: incident.SeverityAverage, Status: incident.IncidentStatusAcknowledged, Value: 1, Threshold: 0, Age: 6*time.Hour + 12*time.Minute, AckBy: "admin", RuleName: "Backup lag"},
		{DisplayID: "INC-8395", Host: "db-core-01", Metric: "availability", Problem: "Hôte hors ligne", Severity: incident.SeverityCritical, Status: incident.IncidentStatusOpen, Value: 0, Threshold: 1, Age: 35 * time.Minute, RuleName: "Hôte hors ligne"},
	}

	for _, d := range defs {
		srv, ok := byName[d.Host]
		if !ok {
			continue
		}
		started := now.Add(-d.Age)
		inc := incident.Incident{
			ID:            incident.NewIncidentID(),
			DisplayID:     d.DisplayID,
			ServerID:      srv.ID,
			ServerName:    srv.Name,
			Metric:        d.Metric,
			Problem:       d.Problem,
			Severity:      d.Severity,
			RuleName:      d.RuleName,
			Status:        d.Status,
			CurrentValue:  d.Value,
			Threshold:     d.Threshold,
			StartedAt:     started,
			LastUpdatedAt: now.Add(-2 * time.Minute),
		}
		if d.Status == incident.IncidentStatusAcknowledged {
			ack := now.Add(-20 * time.Minute)
			inc.AcknowledgedAt = &ack
			inc.AcknowledgedBy = d.AckBy
		}
		if err := repo.Create(ctx, inc); err != nil {
			return fmt.Errorf("seed incident %s: %w", d.DisplayID, err)
		}
	}
	return nil
}

func seedDocker(rt *dockerruntime.DemoRuntime, serverID server.ServerID, hostName string) {
	now := time.Now().UTC()
	started := now.Add(-48 * time.Hour)
	containers := []dockerdomain.Container{
		{
			ID: dockerdomain.ContainerID(shared.NewID()[:12]), ServerID: serverID,
			Name: hostName + "-app", Image: "ghcr.io/hostdeck/app:1.4.2",
			Status: dockerdomain.ContainerStatusRunning, State: "running",
			Ports: "0.0.0.0:8080->8080/tcp", Networks: "bridge", Command: "/app/server",
			RestartCount: 0, CPUPercent: 12.4, MemoryUsed: 256 << 20, MemoryLimit: 1 << 30,
			NetworkRX: 1.2e6, NetworkTX: 0.8e6, CreatedAt: now.Add(-30 * 24 * time.Hour), StartedAt: &started,
		},
		{
			ID: dockerdomain.ContainerID(shared.NewID()[:12]), ServerID: serverID,
			Name: hostName + "-sidecar", Image: "prom/node-exporter:v1.7.0",
			Status: dockerdomain.ContainerStatusRunning, State: "running",
			Ports: "9100/tcp", Networks: "bridge", Command: "/bin/node_exporter",
			RestartCount: 1, CPUPercent: 1.2, MemoryUsed: 32 << 20, MemoryLimit: 128 << 20,
			CreatedAt: now.Add(-60 * 24 * time.Hour), StartedAt: &started,
		},
	}
	info := dockerdomain.HostInfo{
		ServerID: serverID, DockerVersion: "24.0.7", APIVersion: "1.43",
		OS: "linux", Architecture: "x86_64", Containers: 2, ContainersRunning: 2,
		Images: 18, NCPU: 4, MemTotal: 16 << 30,
	}
	rt.SeedServer(serverID, containers, info)
}

func clamp(v, min, max float64) float64 {
	if v < min {
		return min
	}
	if v > max {
		return max
	}
	return v
}
