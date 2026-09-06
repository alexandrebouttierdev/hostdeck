package ssh

import (
	"context"
	"fmt"
	"sync"

	"github.com/alexandrebouttierdev/hostdeck/internal/application/ports"
	"github.com/alexandrebouttierdev/hostdeck/internal/domain/monitoring"
	"github.com/alexandrebouttierdev/hostdeck/internal/domain/server"
	"github.com/alexandrebouttierdev/hostdeck/internal/domain/shared"
)

// HostCollector collects host metrics over SSH using SystemCollector.
// It keeps per-server delta state so CPU/network rates remain meaningful.
type HostCollector struct {
	factory ports.SSHConnectionFactory

	mu         sync.Mutex
	collectors map[server.ServerID]*SystemCollector
}

// NewHostCollector constructs an SSH-backed HostMetricCollector.
func NewHostCollector(factory ports.SSHConnectionFactory) *HostCollector {
	return &HostCollector{
		factory:    factory,
		collectors: make(map[server.ServerID]*SystemCollector),
	}
}

// Collect connects via SSH, runs SystemCollector.Collect, and returns a sample.
func (c *HostCollector) Collect(ctx context.Context, srv server.Server) (monitoring.MetricSample, error) {
	if c == nil || c.factory == nil {
		return monitoring.MetricSample{}, fmt.Errorf("ssh host collector: factory is nil")
	}
	conn, err := c.factory.Connect(ctx, srv)
	if err != nil {
		return monitoring.MetricSample{}, err
	}
	defer func() { _ = conn.Close() }()

	c.mu.Lock()
	col, ok := c.collectors[srv.ID]
	if !ok {
		col = NewSystemCollector(conn)
		c.collectors[srv.ID] = col
	} else {
		col.Bind(conn)
	}
	c.mu.Unlock()

	sample, err := col.Collect(ctx)
	if err != nil {
		return sample, err
	}
	if sample.ID == "" {
		sample.ID = shared.NewID()
	}
	sample.ServerID = srv.ID
	return sample, nil
}
