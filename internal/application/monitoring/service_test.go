package monitoring

import (
	"context"
	"errors"
	"sync"
	"testing"
	"time"

	"github.com/alexandrebouttierdev/hostdeck/internal/application/ports"
	"github.com/alexandrebouttierdev/hostdeck/internal/domain/incident"
	domainmon "github.com/alexandrebouttierdev/hostdeck/internal/domain/monitoring"
	"github.com/alexandrebouttierdev/hostdeck/internal/domain/server"
	"github.com/alexandrebouttierdev/hostdeck/internal/domain/shared"
)

func TestEvaluateRule(t *testing.T) {
	sample := domainmon.MetricSample{
		CPUTotalPercent:  85,
		MemoryUsedBytes:  9 << 30,
		MemoryTotalBytes: 10 << 30,
		DiskUsedBytes:    70,
		DiskTotalBytes:   100,
		Load1:            2.5,
		Load5:            1.8,
		Load15:           1.1,
	}

	tests := []struct {
		name string
		rule incident.AlertRule
		want bool
	}{
		{
			name: "cpu above threshold",
			rule: incident.AlertRule{Enabled: true, Metric: "cpu_total", Operator: ">", Threshold: 80},
			want: true,
		},
		{
			name: "cpu below threshold",
			rule: incident.AlertRule{Enabled: true, Metric: "cpu_total", Operator: ">", Threshold: 90},
			want: false,
		},
		{
			name: "memory percent",
			rule: incident.AlertRule{Enabled: true, Metric: "memory_used_percent", Operator: ">=", Threshold: 90},
			want: true,
		},
		{
			name: "disk percent",
			rule: incident.AlertRule{Enabled: true, Metric: "disk_used_percent", Operator: ">", Threshold: 70},
			want: false,
		},
		{
			name: "load1",
			rule: incident.AlertRule{Enabled: true, Metric: "load1", Operator: ">", Threshold: 2},
			want: true,
		},
		{
			name: "disabled rule",
			rule: incident.AlertRule{Enabled: false, Metric: "cpu_total", Operator: ">", Threshold: 1},
			want: false,
		},
		{
			name: "unknown metric",
			rule: incident.AlertRule{Enabled: true, Metric: "http_5xx_rate", Operator: ">", Threshold: 1},
			want: false,
		},
		{
			name: "lte operator",
			rule: incident.AlertRule{Enabled: true, Metric: "load15", Operator: "<=", Threshold: 1.1},
			want: true,
		},
	}

	for _, tt := range tests {
		t.Run(tt.name, func(t *testing.T) {
			got := EvaluateRule(sample, tt.rule)
			if got != tt.want {
				t.Fatalf("EvaluateRule() = %v, want %v", got, tt.want)
			}
		})
	}
}

func TestMetricValueAliases(t *testing.T) {
	sample := domainmon.MetricSample{CPUTotalPercent: 42}
	v, ok := MetricValue(sample, "CPU")
	if !ok || v != 42 {
		t.Fatalf("MetricValue CPU = %v %v", v, ok)
	}
}

type fixedClock struct{ t time.Time }

func (c fixedClock) Now() time.Time { return c.t }

type memServers struct {
	mu   sync.Mutex
	list []server.Server
}

func (m *memServers) List(ctx context.Context) ([]server.Server, error) {
	m.mu.Lock()
	defer m.mu.Unlock()
	out := make([]server.Server, len(m.list))
	copy(out, m.list)
	return out, nil
}
func (m *memServers) Get(ctx context.Context, id server.ServerID) (server.Server, error) {
	m.mu.Lock()
	defer m.mu.Unlock()
	for _, s := range m.list {
		if s.ID == id {
			return s, nil
		}
	}
	return server.Server{}, shared.ErrNotFound
}
func (m *memServers) Create(ctx context.Context, srv server.Server) error {
	m.mu.Lock()
	defer m.mu.Unlock()
	m.list = append(m.list, srv)
	return nil
}
func (m *memServers) Update(ctx context.Context, srv server.Server) error {
	m.mu.Lock()
	defer m.mu.Unlock()
	for i := range m.list {
		if m.list[i].ID == srv.ID {
			m.list[i] = srv
			return nil
		}
	}
	return shared.ErrNotFound
}
func (m *memServers) Delete(ctx context.Context, id server.ServerID) error { return nil }

type memMetrics struct {
	mu      sync.Mutex
	samples []domainmon.MetricSample
}

func (m *memMetrics) SaveSample(ctx context.Context, sample domainmon.MetricSample) error {
	m.mu.Lock()
	defer m.mu.Unlock()
	m.samples = append(m.samples, sample)
	return nil
}
func (m *memMetrics) Latest(ctx context.Context, serverID server.ServerID) (domainmon.MetricSample, error) {
	m.mu.Lock()
	defer m.mu.Unlock()
	for i := len(m.samples) - 1; i >= 0; i-- {
		if m.samples[i].ServerID == serverID {
			return m.samples[i], nil
		}
	}
	return domainmon.MetricSample{}, shared.ErrNotFound
}
func (m *memMetrics) History(ctx context.Context, serverID server.ServerID, from, to time.Time, limit int) ([]domainmon.MetricSample, error) {
	return nil, nil
}
func (m *memMetrics) PruneBefore(ctx context.Context, before time.Time) (int64, error) { return 0, nil }

type memIncidents struct {
	mu   sync.Mutex
	list []incident.Incident
}

func (m *memIncidents) List(ctx context.Context, onlyActive bool) ([]incident.Incident, error) {
	m.mu.Lock()
	defer m.mu.Unlock()
	var out []incident.Incident
	for _, inc := range m.list {
		if onlyActive && inc.Status != incident.IncidentStatusOpen && inc.Status != incident.IncidentStatusAcknowledged {
			continue
		}
		out = append(out, inc)
	}
	return out, nil
}
func (m *memIncidents) Get(ctx context.Context, id incident.IncidentID) (incident.Incident, error) {
	return incident.Incident{}, shared.ErrNotFound
}
func (m *memIncidents) Create(ctx context.Context, inc incident.Incident) error {
	m.mu.Lock()
	defer m.mu.Unlock()
	m.list = append(m.list, inc)
	return nil
}
func (m *memIncidents) Update(ctx context.Context, inc incident.Incident) error {
	m.mu.Lock()
	defer m.mu.Unlock()
	for i := range m.list {
		if m.list[i].ID == inc.ID {
			m.list[i] = inc
			return nil
		}
	}
	return shared.ErrNotFound
}

type memAlerts struct {
	mu    sync.Mutex
	rules []incident.AlertRule
}

func (m *memAlerts) List(ctx context.Context) ([]incident.AlertRule, error) {
	m.mu.Lock()
	defer m.mu.Unlock()
	out := make([]incident.AlertRule, len(m.rules))
	copy(out, m.rules)
	return out, nil
}
func (m *memAlerts) Get(ctx context.Context, id string) (incident.AlertRule, error) {
	return incident.AlertRule{}, shared.ErrNotFound
}
func (m *memAlerts) Create(ctx context.Context, rule incident.AlertRule) error { return nil }
func (m *memAlerts) Update(ctx context.Context, rule incident.AlertRule) error {
	m.mu.Lock()
	defer m.mu.Unlock()
	for i := range m.rules {
		if m.rules[i].ID == rule.ID {
			m.rules[i] = rule
			return nil
		}
	}
	return nil
}
func (m *memAlerts) Delete(ctx context.Context, id string) error { return nil }

type fakeCollector struct {
	sample domainmon.MetricSample
	err    error
}

func (f fakeCollector) Collect(ctx context.Context, srv server.Server) (domainmon.MetricSample, error) {
	if f.err != nil {
		return domainmon.MetricSample{}, f.err
	}
	s := f.sample
	s.ServerID = srv.ID
	return s, nil
}

type memNotifier struct {
	mu    sync.Mutex
	calls int
}

func (n *memNotifier) Notify(ctx context.Context, title, body string) error {
	n.mu.Lock()
	defer n.mu.Unlock()
	n.calls++
	return nil
}

func TestFleetTickCreatesIncident(t *testing.T) {
	now := time.Date(2026, 9, 6, 12, 0, 0, 0, time.UTC)
	sid := server.NewServerID()
	servers := &memServers{list: []server.Server{{
		ID: sid, Name: "web-01", Host: "10.0.0.1", Port: 22, Username: "root",
		Status: server.ServerStatusOnline, MonitoringEnabled: true, Group: "web",
	}}}
	metrics := &memMetrics{}
	incidents := &memIncidents{}
	alerts := &memAlerts{rules: []incident.AlertRule{{
		ID: "rule-cpu", Name: "CPU élevée > 80%", Enabled: true,
		Metric: "cpu_total", Operator: ">", Threshold: 80,
		Severity: incident.SeverityHigh, Scope: "all",
	}}}
	notifier := &memNotifier{}
	collector := fakeCollector{sample: domainmon.MetricSample{
		CPUTotalPercent: 92,
		CollectedAt:     now,
	}}

	svc := NewServiceWithDeps(
		metrics, servers, fixedClock{t: now}, collector,
		alerts, incidents, notifier, nil, nil,
	)
	svc.fleetTick(context.Background())

	if len(metrics.samples) != 1 {
		t.Fatalf("expected 1 sample, got %d", len(metrics.samples))
	}
	list, err := incidents.List(context.Background(), true)
	if err != nil {
		t.Fatal(err)
	}
	if len(list) != 1 {
		t.Fatalf("expected 1 incident, got %d", len(list))
	}
	if list[0].ServerID != sid || list[0].RuleID != "rule-cpu" {
		t.Fatalf("unexpected incident: %+v", list[0])
	}
	if notifier.calls != 1 {
		t.Fatalf("expected 1 notification, got %d", notifier.calls)
	}

	// Second tick with same breach should not create a duplicate incident.
	svc.fleetTick(context.Background())
	list, _ = incidents.List(context.Background(), true)
	if len(list) != 1 {
		t.Fatalf("expected still 1 incident, got %d", len(list))
	}
}

func TestFleetTickRecoversIncident(t *testing.T) {
	now := time.Date(2026, 9, 6, 12, 0, 0, 0, time.UTC)
	sid := server.NewServerID()
	servers := &memServers{list: []server.Server{{
		ID: sid, Name: "web-01", Host: "10.0.0.1", Port: 22, Username: "root",
		Status: server.ServerStatusOnline, MonitoringEnabled: true,
	}}}
	metrics := &memMetrics{}
	ruleID := "rule-cpu"
	incidents := &memIncidents{list: []incident.Incident{{
		ID: incident.NewIncidentID(), DisplayID: "INC-TEST",
		ServerID: sid, ServerName: "web-01", Metric: "cpu_total",
		RuleID: ruleID, RuleName: "CPU élevée > 80%",
		Status: incident.IncidentStatusOpen, CurrentValue: 95, Threshold: 80,
		StartedAt: now.Add(-time.Hour), LastUpdatedAt: now.Add(-time.Minute),
	}}}
	alerts := &memAlerts{rules: []incident.AlertRule{{
		ID: ruleID, Name: "CPU élevée > 80%", Enabled: true,
		Metric: "cpu_total", Operator: ">", Threshold: 80,
		Severity: incident.SeverityHigh, Scope: "all",
	}}}

	svc := NewServiceWithDeps(
		metrics, servers, fixedClock{t: now},
		fakeCollector{sample: domainmon.MetricSample{CPUTotalPercent: 20, CollectedAt: now}},
		alerts, incidents, &memNotifier{}, nil, nil,
	)
	svc.fleetTick(context.Background())

	active, _ := incidents.List(context.Background(), true)
	if len(active) != 0 {
		t.Fatalf("expected recovered (no active), got %d", len(active))
	}
	all, _ := incidents.List(context.Background(), false)
	if len(all) != 1 || all[0].Status != incident.IncidentStatusRecovered {
		t.Fatalf("expected recovered status, got %+v", all)
	}
}

func TestFleetTickCollectFailureMarksOffline(t *testing.T) {
	now := time.Date(2026, 9, 6, 12, 0, 0, 0, time.UTC)
	sid := server.NewServerID()
	servers := &memServers{list: []server.Server{{
		ID: sid, Name: "db-01", Host: "10.0.0.2", Port: 22, Username: "root",
		Status: server.ServerStatusOnline, MonitoringEnabled: true,
	}}}
	svc := NewServiceWithDeps(
		&memMetrics{}, servers, fixedClock{t: now},
		fakeCollector{err: errors.New("ssh dial failed")},
		&memAlerts{}, &memIncidents{}, nil, nil, nil,
	)
	svc.fleetTick(context.Background())

	got, err := servers.Get(context.Background(), sid)
	if err != nil {
		t.Fatal(err)
	}
	if got.Status != server.ServerStatusOffline {
		t.Fatalf("status = %s, want offline", got.Status)
	}
}

func TestFallbackHostCollector(t *testing.T) {
	fb := FallbackHostCollector{
		Primary:  fakeCollector{err: errors.New("fail")},
		Fallback: fakeCollector{sample: domainmon.MetricSample{CPUTotalPercent: 11}},
	}
	sample, err := fb.Collect(context.Background(), server.Server{ID: server.NewServerID()})
	if err != nil {
		t.Fatal(err)
	}
	if sample.CPUTotalPercent != 11 {
		t.Fatalf("cpu = %v", sample.CPUTotalPercent)
	}
}

var (
	_ ports.ServerRepository           = (*memServers)(nil)
	_ ports.MetricsRepository          = (*memMetrics)(nil)
	_ ports.IncidentRepository         = (*memIncidents)(nil)
	_ ports.AlertRuleRepository        = (*memAlerts)(nil)
	_ ports.HostMetricCollector        = fakeCollector{}
	_ ports.DesktopNotificationService = (*memNotifier)(nil)
)
