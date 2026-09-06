// Package monitoring implements metrics query and fleet coordinator use cases.
package monitoring

import (
	"context"
	"sync"
	"time"

	"github.com/alexandrebouttierdev/hostdeck/internal/application/dto"
	"github.com/alexandrebouttierdev/hostdeck/internal/application/ports"
	"github.com/alexandrebouttierdev/hostdeck/internal/domain/monitoring"
	"github.com/alexandrebouttierdev/hostdeck/internal/domain/server"
	"github.com/alexandrebouttierdev/hostdeck/internal/domain/shared"
)

// Service groups monitoring use cases.
type Service struct {
	metrics ports.MetricsRepository
	servers ports.ServerRepository
	clock   ports.Clock

	mu      sync.Mutex
	running bool
	cancel  context.CancelFunc
}

// NewService constructs a monitoring Service.
func NewService(metrics ports.MetricsRepository, servers ports.ServerRepository, clock ports.Clock) *Service {
	if clock == nil {
		clock = ports.SystemClock()
	}
	return &Service{metrics: metrics, servers: servers, clock: clock}
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
		from = to.Add(-monitoring.TimeRange(in.Range).Duration())
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
	samples := make([]monitoring.MetricSample, 0, len(hist.Points))
	for _, p := range hist.Points {
		samples = append(samples, monitoring.MetricSample{
			ServerID:        id,
			CollectedAt:     p.CollectedAt,
			CPUTotalPercent: p.CPUTotalPercent,
			Load1:           p.Load1,
			// Reconstruct percentages via used/total placeholders for stats helper.
			MemoryUsedBytes:  uint64(p.MemoryUsedPercent),
			MemoryTotalBytes: 100,
			DiskUsedBytes:    uint64(p.DiskUsedPercent),
			DiskTotalBytes:   100,
		})
	}
	return dto.ComputeMetricStatistics(in.ServerID, samples), nil
}

// StartFleet starts the fleet monitoring coordinator skeleton.
// It marks the service as running; actual collection is wired later.
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
	ticker := time.NewTicker(30 * time.Second)
	defer ticker.Stop()
	for {
		select {
		case <-ctx.Done():
			return
		case <-ticker.C:
			// Skeleton: future work collects metrics per enabled server.
			if s.servers == nil {
				continue
			}
			_, _ = s.servers.List(ctx)
		}
	}
}
