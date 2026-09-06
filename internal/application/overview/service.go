// Package overview builds the fleet overview dashboard DTO.
package overview

import (
	"context"

	"github.com/alexandrebouttierdev/hostdeck/internal/application/dto"
	"github.com/alexandrebouttierdev/hostdeck/internal/application/ports"
	"github.com/alexandrebouttierdev/hostdeck/internal/domain/incident"
	"github.com/alexandrebouttierdev/hostdeck/internal/domain/server"
)

// Service builds overview statistics from repositories.
type Service struct {
	servers   ports.ServerRepository
	metrics   ports.MetricsRepository
	incidents ports.IncidentRepository
	alerts    ports.AlertRuleRepository
}

// NewService constructs an overview Service.
func NewService(
	servers ports.ServerRepository,
	metrics ports.MetricsRepository,
	incidents ports.IncidentRepository,
	alerts ports.AlertRuleRepository,
) *Service {
	return &Service{
		servers:   servers,
		metrics:   metrics,
		incidents: incidents,
		alerts:    alerts,
	}
}

// Build aggregates overview stats.
func (s *Service) Build(ctx context.Context) (dto.OverviewDTO, error) {
	hosts, err := s.servers.List(ctx)
	if err != nil {
		return dto.OverviewDTO{}, err
	}

	out := dto.OverviewDTO{
		TotalHosts:       len(hosts),
		CollectionActive: true,
	}

	type scored struct {
		summary dto.ServerSummaryDTO
		cpu     float64
	}
	var scoredHosts []scored
	var cpuSum, memSum, diskSum float64
	var metricCount int

	for _, srv := range hosts {
		switch srv.Status {
		case server.ServerStatusOnline:
			out.OnlineHosts++
		case server.ServerStatusWarning:
			out.WarningHosts++
		case server.ServerStatusMaintenance:
			out.MaintenanceHosts++
		case server.ServerStatusOffline, server.ServerStatusAuthenticationFailed, server.ServerStatusGatewayUnavailable:
			out.OfflineHosts++
			out.ProblemHosts++
		default:
			out.ProblemHosts++
		}

		summary := dto.FromServer(srv)
		if s.metrics != nil {
			if sample, err := s.metrics.Latest(ctx, srv.ID); err == nil {
				dto.ApplyMetricsToSummary(&summary, sample)
				cpuSum += sample.CPUTotalPercent
				memSum += sample.MemoryUsedPercent()
				diskSum += sample.DiskUsedPercent()
				metricCount++
				scoredHosts = append(scoredHosts, scored{summary: summary, cpu: sample.CPUTotalPercent})
			}
		}
	}

	if out.TotalHosts > 0 {
		healthy := out.OnlineHosts + out.WarningHosts
		out.AvailabilityPct = float64(healthy) / float64(out.TotalHosts) * 100
	}
	if metricCount > 0 {
		n := float64(metricCount)
		out.GlobalCPUPercent = cpuSum / n
		out.GlobalMemoryPercent = memSum / n
		out.GlobalDiskPercent = diskSum / n
	}

	// Top hosts by CPU.
	for i := 0; i < len(scoredHosts); i++ {
		for j := i + 1; j < len(scoredHosts); j++ {
			if scoredHosts[j].cpu > scoredHosts[i].cpu {
				scoredHosts[i], scoredHosts[j] = scoredHosts[j], scoredHosts[i]
			}
		}
	}
	limit := 8
	if len(scoredHosts) < limit {
		limit = len(scoredHosts)
	}
	for i := 0; i < limit; i++ {
		out.TopHosts = append(out.TopHosts, scoredHosts[i].summary)
	}

	if s.incidents != nil {
		active, err := s.incidents.List(ctx, true)
		if err != nil {
			return dto.OverviewDTO{}, err
		}
		out.ActiveIncidents = len(active)
		for _, inc := range active {
			switch inc.Severity {
			case incident.SeverityCritical:
				out.CriticalIncidents++
			case incident.SeverityHigh:
				out.HighIncidents++
			case incident.SeverityWarning:
				out.WarningIncidents++
			case incident.SeverityAverage, incident.SeverityInformation:
				out.AverageIncidents++
			}
		}
		recentLimit := 10
		if len(active) < recentLimit {
			recentLimit = len(active)
		}
		for i := 0; i < recentLimit; i++ {
			out.RecentIncidents = append(out.RecentIncidents, dto.FromIncident(active[i]))
		}
	}

	if s.alerts != nil {
		rules, err := s.alerts.List(ctx)
		if err != nil {
			return dto.OverviewDTO{}, err
		}
		for _, r := range rules {
			if r.Enabled {
				out.OpenAlertRules++
			}
		}
	}

	return out, nil
}
