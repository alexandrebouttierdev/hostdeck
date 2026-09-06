package demo

import (
	"context"
	"errors"
	"fmt"
	"math"
	"math/rand/v2"
	"time"

	"github.com/alexandrebouttierdev/hostdeck/internal/application/ports"
	"github.com/alexandrebouttierdev/hostdeck/internal/domain/monitoring"
	"github.com/alexandrebouttierdev/hostdeck/internal/domain/server"
	"github.com/alexandrebouttierdev/hostdeck/internal/domain/shared"
)

// MetricCollector produces synthetic metric samples with a small random walk
// from the last stored sample. Used when SeedDemo hosts are not SSH-reachable.
type MetricCollector struct {
	metrics ports.MetricsRepository
	clock   ports.Clock
	rng     *rand.Rand
}

// NewMetricCollector constructs a demo HostMetricCollector.
func NewMetricCollector(metrics ports.MetricsRepository, clock ports.Clock) *MetricCollector {
	if clock == nil {
		clock = ports.SystemClock()
	}
	return &MetricCollector{
		metrics: metrics,
		clock:   clock,
		rng:     rand.New(rand.NewPCG(uint64(time.Now().UnixNano()), 0x484f53544445434b)),
	}
}

// Collect returns a drifted sample based on the latest stored metrics.
// Hosts marked offline / auth-failed / gateway-unavailable stay unreachable.
func (c *MetricCollector) Collect(ctx context.Context, srv server.Server) (monitoring.MetricSample, error) {
	switch srv.Status {
	case server.ServerStatusOffline, server.ServerStatusAuthenticationFailed, server.ServerStatusGatewayUnavailable:
		return monitoring.MetricSample{}, fmt.Errorf("demo host unreachable: %s", srv.Status)
	}
	base, err := c.metrics.Latest(ctx, srv.ID)
	if err != nil {
		if !errors.Is(err, shared.ErrNotFound) {
			return monitoring.MetricSample{}, err
		}
		base = baselineSample(srv, c.clock.Now())
	}
	return driftSample(base, srv.ID, c.clock.Now(), c.rng), nil
}

func baselineSample(srv server.Server, now time.Time) monitoring.MetricSample {
	memTotal := uint64(16 << 30)
	diskTotal := uint64(500 << 30)
	return monitoring.MetricSample{
		ID:               shared.NewID(),
		ServerID:         srv.ID,
		CollectedAt:      now,
		CPUTotalPercent:  25,
		CPUUserPercent:   16,
		CPUSystemPercent: 5,
		CPUIOWaitPercent: 2,
		MemoryTotalBytes: memTotal,
		MemoryUsedBytes:  memTotal / 2,
		MemoryFreeBytes:  memTotal / 4,
		MemoryCacheBytes: memTotal / 8,
		SwapTotalBytes:   2 << 30,
		Load1:            0.5,
		Load5:            0.4,
		Load15:           0.3,
		DiskTotalBytes:   diskTotal,
		DiskUsedBytes:    diskTotal / 3,
		UptimeSeconds:    86400,
	}
}

func driftSample(base monitoring.MetricSample, id server.ServerID, now time.Time, rng *rand.Rand) monitoring.MetricSample {
	sample := base
	sample.ID = shared.NewID()
	sample.ServerID = id
	sample.CollectedAt = now

	sample.CPUTotalPercent = clamp(base.CPUTotalPercent+rng.Float64()*4-2, 1, 99)
	sample.CPUUserPercent = sample.CPUTotalPercent * 0.65
	sample.CPUSystemPercent = sample.CPUTotalPercent * 0.2
	sample.CPUIOWaitPercent = sample.CPUTotalPercent * 0.1
	sample.CPUNicePercent = sample.CPUTotalPercent * 0.03
	sample.CPUStealPercent = sample.CPUTotalPercent * 0.02

	memPct := clamp(base.MemoryUsedPercent()+rng.Float64()*2-1, 5, 98)
	if sample.MemoryTotalBytes == 0 {
		sample.MemoryTotalBytes = 16 << 30
	}
	sample.MemoryUsedBytes = uint64(memPct / 100 * float64(sample.MemoryTotalBytes))
	sample.MemoryFreeBytes = sample.MemoryTotalBytes - sample.MemoryUsedBytes

	diskPct := clamp(base.DiskUsedPercent()+rng.Float64()*0.4-0.1, 5, 98)
	if sample.DiskTotalBytes == 0 {
		sample.DiskTotalBytes = 500 << 30
	}
	sample.DiskUsedBytes = uint64(diskPct / 100 * float64(sample.DiskTotalBytes))

	sample.Load1 = math.Max(0, base.Load1+rng.Float64()*0.2-0.1)
	sample.Load5 = math.Max(0, base.Load5+rng.Float64()*0.15-0.075)
	sample.Load15 = math.Max(0, base.Load15+rng.Float64()*0.1-0.05)

	sample.NetworkRXBytesPerSec = math.Max(0, base.NetworkRXBytesPerSec*(0.9+rng.Float64()*0.2))
	sample.NetworkTXBytesPerSec = math.Max(0, base.NetworkTXBytesPerSec*(0.9+rng.Float64()*0.2))
	sample.UptimeSeconds = base.UptimeSeconds + 30
	return sample
}
