package monitoring

import (
	"context"
	"fmt"
	"strings"
	"sync"
	"time"

	"github.com/alexandrebouttierdev/hostdeck/internal/application/dto"
	"github.com/alexandrebouttierdev/hostdeck/internal/application/ports"
	"github.com/alexandrebouttierdev/hostdeck/internal/domain/incident"
	domainmon "github.com/alexandrebouttierdev/hostdeck/internal/domain/monitoring"
	"github.com/alexandrebouttierdev/hostdeck/internal/domain/server"
	"github.com/alexandrebouttierdev/hostdeck/internal/domain/shared"
)

const (
	defaultFleetInterval = 30 * time.Second
	maxFleetConcurrency  = 8
)

// Logger is an optional structured logger used by the fleet coordinator.
type Logger interface {
	Debug(msg string, args ...any)
	Info(msg string, args ...any)
	Warn(msg string, args ...any)
	Error(msg string, args ...any)
}

// Service groups monitoring use cases and the fleet coordinator.
type Service struct {
	metrics   ports.MetricsRepository
	servers   ports.ServerRepository
	clock     ports.Clock
	collector ports.HostMetricCollector
	alerts    ports.AlertRuleRepository
	incidents ports.IncidentRepository
	notifier  ports.DesktopNotificationService
	settings  ports.SettingsRepository
	log       Logger

	mu      sync.Mutex
	running bool
	cancel  context.CancelFunc
}

// Option configures optional Service dependencies.
type Option func(*Service)

// WithCollector sets the host metric collector used by the fleet loop.
func WithCollector(c ports.HostMetricCollector) Option {
	return func(s *Service) { s.collector = c }
}

// WithAlerts sets the alert rule repository.
func WithAlerts(r ports.AlertRuleRepository) Option {
	return func(s *Service) { s.alerts = r }
}

// WithIncidents sets the incident repository.
func WithIncidents(r ports.IncidentRepository) Option {
	return func(s *Service) { s.incidents = r }
}

// WithNotifier sets the desktop notification sink.
func WithNotifier(n ports.DesktopNotificationService) Option {
	return func(s *Service) { s.notifier = n }
}

// WithSettings sets the settings repository (fleet poll interval).
func WithSettings(r ports.SettingsRepository) Option {
	return func(s *Service) { s.settings = r }
}

// WithLogger sets an optional logger.
func WithLogger(l Logger) Option {
	return func(s *Service) { s.log = l }
}

// NewService constructs a monitoring Service.
// Optional dependencies are passed via Option (backward compatible with the 3-arg form).
func NewService(metrics ports.MetricsRepository, servers ports.ServerRepository, clock ports.Clock, opts ...Option) *Service {
	if clock == nil {
		clock = ports.SystemClock()
	}
	s := &Service{metrics: metrics, servers: servers, clock: clock}
	for _, opt := range opts {
		if opt != nil {
			opt(s)
		}
	}
	return s
}

// NewServiceWithDeps constructs a fully wired fleet monitoring Service.
func NewServiceWithDeps(
	metrics ports.MetricsRepository,
	servers ports.ServerRepository,
	clock ports.Clock,
	collector ports.HostMetricCollector,
	alerts ports.AlertRuleRepository,
	incidents ports.IncidentRepository,
	notifier ports.DesktopNotificationService,
	settings ports.SettingsRepository,
	log Logger,
) *Service {
	return NewService(metrics, servers, clock,
		WithCollector(collector),
		WithAlerts(alerts),
		WithIncidents(incidents),
		WithNotifier(notifier),
		WithSettings(settings),
		WithLogger(log),
	)
}

// FallbackHostCollector tries Primary then Fallback (e.g. SSH then demo).
type FallbackHostCollector struct {
	Primary  ports.HostMetricCollector
	Fallback ports.HostMetricCollector
}

// Collect tries the primary collector, then the fallback on error.
func (c FallbackHostCollector) Collect(ctx context.Context, srv server.Server) (domainmon.MetricSample, error) {
	if c.Primary != nil {
		sample, err := c.Primary.Collect(ctx, srv)
		if err == nil {
			return sample, nil
		}
		if c.Fallback == nil {
			return sample, err
		}
	}
	if c.Fallback == nil {
		return domainmon.MetricSample{}, fmt.Errorf("no host metric collector configured")
	}
	return c.Fallback.Collect(ctx, srv)
}

// GetLatest returns the latest metrics for a server.
func (s *Service) GetLatest(ctx context.Context, serverID string) (dto.LatestMetricDTO, error) {
	id, err := server.ParseServerID(serverID)
	if err != nil {
		return dto.LatestMetricDTO{}, shared.NewValidationError("server_id", "invalid server id")
	}
	sample, err := s.metrics.Latest(ctx, id)
	if err != nil {
		return dto.LatestMetricDTO{}, err
	}
	return dto.FromMetricSample(sample), nil
}

// GetHistory returns metric history for a server and time range.
func (s *Service) GetHistory(ctx context.Context, in dto.MetricHistoryRequestDTO) (dto.MetricHistoryDTO, error) {
	id, err := server.ParseServerID(in.ServerID)
	if err != nil {
		return dto.MetricHistoryDTO{}, shared.NewValidationError("server_id", "invalid server id")
	}

	to := s.clock.Now()
	from := to.Add(-6 * time.Hour)
	if in.Range != "" {
		from = to.Add(-domainmon.TimeRange(in.Range).Duration())
	}
	if in.From != nil {
		from = *in.From
	}
	if in.To != nil {
		to = *in.To
	}
	limit := in.Limit
	if limit <= 0 {
		limit = 500
	}

	samples, err := s.metrics.History(ctx, id, from, to, limit)
	if err != nil {
		return dto.MetricHistoryDTO{}, err
	}
	return dto.FromMetricHistory(id.String(), from, to, samples), nil
}

// GetStatistics computes aggregates over a history window.
func (s *Service) GetStatistics(ctx context.Context, in dto.MetricHistoryRequestDTO) (dto.MetricStatisticsDTO, error) {
	hist, err := s.GetHistory(ctx, in)
	if err != nil {
		return dto.MetricStatisticsDTO{}, err
	}
	id, _ := server.ParseServerID(in.ServerID)
	samples := make([]domainmon.MetricSample, 0, len(hist.Points))
	for _, p := range hist.Points {
		samples = append(samples, domainmon.MetricSample{
			ServerID:         id,
			CollectedAt:      p.CollectedAt,
			CPUTotalPercent:  p.CPUTotalPercent,
			Load1:            p.Load1,
			MemoryUsedBytes:  uint64(p.MemoryUsedPercent),
			MemoryTotalBytes: 100,
			DiskUsedBytes:    uint64(p.DiskUsedPercent),
			DiskTotalBytes:   100,
		})
	}
	return dto.ComputeMetricStatistics(in.ServerID, samples), nil
}

// StartFleet starts the fleet monitoring coordinator.
func (s *Service) StartFleet(ctx context.Context) error {
	s.mu.Lock()
	defer s.mu.Unlock()
	if s.running {
		return nil
	}
	runCtx, cancel := context.WithCancel(context.Background())
	s.cancel = cancel
	s.running = true
	go s.coordinatorLoop(runCtx)
	return nil
}

// StopFleet stops the fleet coordinator.
func (s *Service) StopFleet(ctx context.Context) error {
	s.mu.Lock()
	defer s.mu.Unlock()
	if !s.running {
		return nil
	}
	if s.cancel != nil {
		s.cancel()
	}
	s.running = false
	s.cancel = nil
	return nil
}

// IsFleetRunning reports coordinator state.
func (s *Service) IsFleetRunning() bool {
	s.mu.Lock()
	defer s.mu.Unlock()
	return s.running
}

func (s *Service) coordinatorLoop(ctx context.Context) {
	interval := s.fleetInterval(ctx)
	ticker := time.NewTicker(interval)
	defer ticker.Stop()

	s.fleetTick(ctx)

	for {
		select {
		case <-ctx.Done():
			return
		case <-ticker.C:
			next := s.fleetInterval(ctx)
			if next != interval {
				ticker.Reset(next)
				interval = next
			}
			s.fleetTick(ctx)
		}
	}
}

func (s *Service) fleetInterval(ctx context.Context) time.Duration {
	if s.settings != nil {
		cfg, err := s.settings.Get(ctx)
		if err == nil {
			if cfg.RefreshIntervalSeconds > 0 {
				return time.Duration(cfg.RefreshIntervalSeconds) * time.Second
			}
			if cfg.DefaultIntervalSeconds > 0 {
				return time.Duration(cfg.DefaultIntervalSeconds) * time.Second
			}
		}
	}
	return defaultFleetInterval
}

// fleetTick collects metrics for all monitoring-enabled servers and evaluates alerts.
func (s *Service) fleetTick(ctx context.Context) {
	if s.servers == nil {
		return
	}
	list, err := s.servers.List(ctx)
	if err != nil {
		s.warn("fleet list servers", "err", err)
		return
	}

	var rules []incident.AlertRule
	if s.alerts != nil {
		rules, err = s.alerts.List(ctx)
		if err != nil {
			s.warn("fleet list alert rules", "err", err)
			rules = nil
		}
	}

	sem := make(chan struct{}, maxFleetConcurrency)
	var wg sync.WaitGroup
	for _, srv := range list {
		if !srv.MonitoringEnabled {
			continue
		}
		if ctx.Err() != nil {
			break
		}
		wg.Add(1)
		go func(srv server.Server) {
			defer wg.Done()
			select {
			case sem <- struct{}{}:
				defer func() { <-sem }()
			case <-ctx.Done():
				return
			}
			s.collectOne(ctx, srv, rules)
		}(srv)
	}
	wg.Wait()
}

func (s *Service) collectOne(ctx context.Context, srv server.Server, rules []incident.AlertRule) {
	if s.collector == nil {
		return
	}
	sample, err := s.collector.Collect(ctx, srv)
	if err != nil {
		s.warn("fleet collect failed", "server", srv.Name, "err", err)
		s.markServerStatus(ctx, srv, server.ServerStatusOffline)
		s.handleCollectFailure(ctx, srv, rules)
		return
	}

	sample.ServerID = srv.ID
	if sample.ID == "" {
		sample.ID = shared.NewID()
	}
	if sample.CollectedAt.IsZero() {
		sample.CollectedAt = s.clock.Now()
	}
	if s.metrics != nil {
		if err := s.metrics.SaveSample(ctx, sample); err != nil {
			s.warn("fleet save sample", "server", srv.Name, "err", err)
			return
		}
	}
	s.markServerCollected(ctx, srv, sample.CollectedAt)
	s.evaluateAlerts(ctx, srv, sample, rules)
}

func (s *Service) markServerStatus(ctx context.Context, srv server.Server, status server.ServerStatus) {
	if s.servers == nil || srv.Status == status {
		return
	}
	srv.Status = status
	srv.UpdatedAt = s.clock.Now()
	if err := s.servers.Update(ctx, srv); err != nil {
		s.warn("fleet update server status", "server", srv.Name, "err", err)
	}
}

func (s *Service) markServerCollected(ctx context.Context, srv server.Server, at time.Time) {
	if s.servers == nil {
		return
	}
	now := at
	if now.IsZero() {
		now = s.clock.Now()
	}
	switch srv.Status {
	case server.ServerStatusOffline, server.ServerStatusAuthenticationFailed,
		server.ServerStatusGatewayUnavailable, server.ServerStatusUnknown, "":
		srv.Status = server.ServerStatusOnline
	}
	srv.LastCollectedAt = &now
	srv.UpdatedAt = now
	if err := s.servers.Update(ctx, srv); err != nil {
		s.warn("fleet update last collected", "server", srv.Name, "err", err)
	}
}

func (s *Service) handleCollectFailure(ctx context.Context, srv server.Server, rules []incident.AlertRule) {
	if s.incidents == nil {
		return
	}
	// Synthetic offline sample for availability rules only.
	offline := domainmon.MetricSample{ServerID: srv.ID, CollectedAt: s.clock.Now()}
	for _, rule := range rules {
		if !rule.Enabled || !ruleApplies(rule, srv) {
			continue
		}
		metric := strings.ToLower(strings.TrimSpace(rule.Metric))
		if metric != "availability" && metric != "online" {
			continue
		}
		// availability == 0 means offline.
		breached := compare(0, rule.Operator, rule.Threshold)
		if !breached {
			continue
		}
		s.openOrUpdateIncident(ctx, srv, rule, 0)
	}
	_ = offline
}

func (s *Service) evaluateAlerts(ctx context.Context, srv server.Server, sample domainmon.MetricSample, rules []incident.AlertRule) {
	if s.incidents == nil || len(rules) == 0 {
		return
	}
	for _, rule := range rules {
		if !rule.Enabled || !ruleApplies(rule, srv) {
			continue
		}
		metric := strings.ToLower(strings.TrimSpace(rule.Metric))
		if metric == "availability" || metric == "online" {
			// Reachable host: recover any open availability incidents.
			if open, ok := s.findOpenIncident(ctx, srv.ID, rule); ok {
				s.recoverIncident(ctx, open, 1)
			}
			continue
		}
		value, ok := MetricValue(sample, rule.Metric)
		if !ok {
			continue
		}
		if EvaluateRule(sample, rule) {
			if s.inCooldown(rule) {
				if open, ok := s.findOpenIncident(ctx, srv.ID, rule); ok {
					open.CurrentValue = value
					open.LastUpdatedAt = s.clock.Now()
					_ = s.incidents.Update(ctx, open)
				}
				continue
			}
			s.openOrUpdateIncident(ctx, srv, rule, value)
		} else if open, ok := s.findOpenIncident(ctx, srv.ID, rule); ok {
			s.recoverIncident(ctx, open, value)
		}
	}
}

func ruleApplies(rule incident.AlertRule, srv server.Server) bool {
	scope := strings.TrimSpace(strings.ToLower(rule.Scope))
	if scope == "" || scope == "all" {
		return true
	}
	return strings.EqualFold(srv.Group, rule.Scope) ||
		strings.EqualFold(srv.Environment, rule.Scope) ||
		strings.EqualFold(srv.Role, rule.Scope)
}

func (s *Service) inCooldown(rule incident.AlertRule) bool {
	if rule.CooldownSeconds <= 0 || rule.LastTriggeredAt == nil {
		return false
	}
	return s.clock.Now().Sub(*rule.LastTriggeredAt) < time.Duration(rule.CooldownSeconds)*time.Second
}

func (s *Service) findOpenIncident(ctx context.Context, serverID server.ServerID, rule incident.AlertRule) (incident.Incident, bool) {
	if s.incidents == nil {
		return incident.Incident{}, false
	}
	list, err := s.incidents.List(ctx, true)
	if err != nil {
		return incident.Incident{}, false
	}
	for _, inc := range list {
		if inc.ServerID != serverID {
			continue
		}
		if rule.ID != "" && inc.RuleID == rule.ID {
			return inc, true
		}
		if inc.RuleName == rule.Name && inc.Metric == rule.Metric {
			return inc, true
		}
	}
	return incident.Incident{}, false
}

func (s *Service) openOrUpdateIncident(ctx context.Context, srv server.Server, rule incident.AlertRule, value float64) {
	now := s.clock.Now()
	if open, ok := s.findOpenIncident(ctx, srv.ID, rule); ok {
		open.CurrentValue = value
		open.LastUpdatedAt = now
		_ = s.incidents.Update(ctx, open)
		return
	}

	inc := incident.Incident{
		ID:            incident.NewIncidentID(),
		DisplayID:     fmt.Sprintf("INC-%s", strings.ToUpper(shared.NewID()[:8])),
		ServerID:      srv.ID,
		ServerName:    srv.Name,
		Metric:        rule.Metric,
		Problem:       rule.Name,
		Severity:      rule.Severity,
		RuleID:        rule.ID,
		RuleName:      rule.Name,
		Status:        incident.IncidentStatusOpen,
		CurrentValue:  value,
		Threshold:     rule.Threshold,
		StartedAt:     now,
		LastUpdatedAt: now,
	}
	if err := s.incidents.Create(ctx, inc); err != nil {
		s.warn("fleet create incident", "server", srv.Name, "rule", rule.Name, "err", err)
		return
	}
	if s.alerts != nil && rule.ID != "" {
		rule.LastTriggeredAt = &now
		rule.Status = "triggered"
		rule.UpdatedAt = now
		_ = s.alerts.Update(ctx, rule)
	}
	if s.notifier != nil {
		title := fmt.Sprintf("Alerte %s — %s", rule.Severity.LabelFR(), srv.Name)
		body := fmt.Sprintf("%s (valeur %.2f, seuil %.2f)", rule.Name, value, rule.Threshold)
		_ = s.notifier.Notify(ctx, title, body)
	}
}

func (s *Service) recoverIncident(ctx context.Context, open incident.Incident, value float64) {
	now := s.clock.Now()
	open.Status = incident.IncidentStatusRecovered
	open.CurrentValue = value
	open.RecoveredAt = &now
	open.LastUpdatedAt = now
	if err := s.incidents.Update(ctx, open); err != nil {
		s.warn("fleet recover incident", "id", open.ID.String(), "err", err)
	}
}

func (s *Service) warn(msg string, args ...any) {
	if s.log != nil {
		s.log.Warn(msg, args...)
	}
}
