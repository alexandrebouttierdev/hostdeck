// Package servers implements server management use cases.
package servers

import (
	"context"
	"fmt"
	"strings"
	"time"

	"github.com/alexandrebouttierdev/hostdeck/internal/application/dto"
	"github.com/alexandrebouttierdev/hostdeck/internal/application/ports"
	"github.com/alexandrebouttierdev/hostdeck/internal/domain/monitoring"
	"github.com/alexandrebouttierdev/hostdeck/internal/domain/server"
	"github.com/alexandrebouttierdev/hostdeck/internal/domain/shared"
)

// Service groups server use cases.
type Service struct {
	servers     ports.ServerRepository
	metrics     ports.MetricsRepository
	credentials ports.CredentialStore
	ssh         ports.SSHConnectionFactory
	clock       ports.Clock
}

// NewService constructs a servers Service.
func NewService(
	servers ports.ServerRepository,
	metrics ports.MetricsRepository,
	credentials ports.CredentialStore,
	ssh ports.SSHConnectionFactory,
	clock ports.Clock,
) *Service {
	if clock == nil {
		clock = ports.SystemClock()
	}
	return &Service{
		servers:     servers,
		metrics:     metrics,
		credentials: credentials,
		ssh:         ssh,
		clock:       clock,
	}
}

// Add creates a new server after validation (including jump-host cycles).
func (s *Service) Add(ctx context.Context, in dto.CreateServerDTO) (dto.ServerDetailsDTO, error) {
	now := s.clock.Now()
	srv := server.Server{
		ID:                server.NewServerID(),
		Name:              strings.TrimSpace(in.Name),
		Host:              strings.TrimSpace(in.Host),
		Port:              in.Port,
		Username:          strings.TrimSpace(in.Username),
		ConnectionMode:    server.ConnectionMode(in.ConnectionMode),
		AuthMethod:        server.AuthMethod(in.AuthMethod),
		Group:             in.Group,
		Environment:       in.Environment,
		Role:              in.Role,
		Tags:              in.Tags,
		Status:            server.ServerStatusUnknown,
		MonitoringEnabled: in.MonitoringEnabled,
		DockerEnabled:     in.DockerEnabled,
		IntervalSeconds:   in.IntervalSeconds,
		CreatedAt:         now,
		UpdatedAt:         now,
	}
	if srv.Port == 0 {
		srv.Port = 22
	}
	if srv.ConnectionMode == "" {
		srv.ConnectionMode = server.ConnectionModeDirect
	}
	if srv.AuthMethod == "" {
		srv.AuthMethod = server.AuthMethodKey
	}
	if in.JumpHostID != "" {
		jid, err := server.ParseServerID(in.JumpHostID)
		if err != nil {
			return dto.ServerDetailsDTO{}, shared.NewValidationError("jump_host_id", "invalid jump host id")
		}
		srv.JumpHostID = &jid
	}
	if err := srv.Validate(); err != nil {
		return dto.ServerDetailsDTO{}, err
	}
	if err := s.validateJumpHost(ctx, srv); err != nil {
		return dto.ServerDetailsDTO{}, err
	}

	srv.CredentialRef = "server:" + srv.ID.String()
	if in.CredentialSecret != "" && s.credentials != nil {
		if err := s.credentials.Set(ctx, srv.CredentialRef, []byte(in.CredentialSecret)); err != nil {
			return dto.ServerDetailsDTO{}, fmt.Errorf("store credential: %w", err)
		}
	}

	if err := s.servers.Create(ctx, srv); err != nil {
		return dto.ServerDetailsDTO{}, err
	}
	return dto.FromServerDetails(srv, nil), nil
}

// Update updates an existing server.
func (s *Service) Update(ctx context.Context, in dto.UpdateServerDTO) (dto.ServerDetailsDTO, error) {
	id, err := server.ParseServerID(in.ID)
	if err != nil {
		return dto.ServerDetailsDTO{}, shared.NewValidationError("id", "invalid server id")
	}
	existing, err := s.servers.Get(ctx, id)
	if err != nil {
		return dto.ServerDetailsDTO{}, err
	}

	existing.Name = strings.TrimSpace(in.Name)
	existing.Host = strings.TrimSpace(in.Host)
	existing.Port = in.Port
	existing.Username = strings.TrimSpace(in.Username)
	existing.ConnectionMode = server.ConnectionMode(in.ConnectionMode)
	existing.AuthMethod = server.AuthMethod(in.AuthMethod)
	existing.Group = in.Group
	existing.Environment = in.Environment
	existing.Role = in.Role
	existing.Tags = in.Tags
	existing.MonitoringEnabled = in.MonitoringEnabled
	existing.DockerEnabled = in.DockerEnabled
	existing.IntervalSeconds = in.IntervalSeconds
	existing.UpdatedAt = s.clock.Now()
	if in.Status != "" {
		existing.Status = server.ServerStatus(in.Status)
	}
	if in.JumpHostID != "" {
		jid, err := server.ParseServerID(in.JumpHostID)
		if err != nil {
			return dto.ServerDetailsDTO{}, shared.NewValidationError("jump_host_id", "invalid jump host id")
		}
		existing.JumpHostID = &jid
	} else if existing.ConnectionMode == server.ConnectionModeDirect {
		existing.JumpHostID = nil
	}

	if err := existing.Validate(); err != nil {
		return dto.ServerDetailsDTO{}, err
	}
	if err := s.validateJumpHost(ctx, existing); err != nil {
		return dto.ServerDetailsDTO{}, err
	}

	if in.CredentialSecret != "" && s.credentials != nil {
		ref := existing.CredentialRef
		if ref == "" {
			ref = "server:" + existing.ID.String()
			existing.CredentialRef = ref
		}
		if err := s.credentials.Set(ctx, ref, []byte(in.CredentialSecret)); err != nil {
			return dto.ServerDetailsDTO{}, fmt.Errorf("store credential: %w", err)
		}
	}

	if err := s.servers.Update(ctx, existing); err != nil {
		return dto.ServerDetailsDTO{}, err
	}
	return dto.FromServerDetails(existing, nil), nil
}

// Delete removes a server and its credential reference.
func (s *Service) Delete(ctx context.Context, id string) error {
	sid, err := server.ParseServerID(id)
	if err != nil {
		return shared.NewValidationError("id", "invalid server id")
	}
	existing, err := s.servers.Get(ctx, sid)
	if err != nil {
		return err
	}
	if err := s.servers.Delete(ctx, sid); err != nil {
		return err
	}
	if s.credentials != nil && existing.CredentialRef != "" {
		_ = s.credentials.Delete(ctx, existing.CredentialRef)
	}
	return nil
}

// GetServers lists all servers with latest metrics and sparklines when available.
func (s *Service) GetServers(ctx context.Context) ([]dto.ServerSummaryDTO, error) {
	list, err := s.servers.List(ctx)
	if err != nil {
		return nil, err
	}
	out := make([]dto.ServerSummaryDTO, 0, len(list))
	for _, srv := range list {
		summary := dto.FromServer(srv)
		if s.metrics != nil {
			if latest, err := s.metrics.Latest(ctx, srv.ID); err == nil {
				dto.ApplyMetricsToSummary(&summary, latest)
				summary.CPUSparkline, summary.MemorySparkline, summary.DiskSparkline, summary.NetworkSparkline =
					s.sparklines(ctx, srv.ID)
			}
		}
		out = append(out, summary)
	}
	return out, nil
}

// GetServerDetails returns one server with latest metrics.
func (s *Service) GetServerDetails(ctx context.Context, id string) (dto.ServerDetailsDTO, error) {
	sid, err := server.ParseServerID(id)
	if err != nil {
		return dto.ServerDetailsDTO{}, shared.NewValidationError("id", "invalid server id")
	}
	srv, err := s.servers.Get(ctx, sid)
	if err != nil {
		return dto.ServerDetailsDTO{}, err
	}
	var latest *monitoring.MetricSample
	if s.metrics != nil {
		if sample, err := s.metrics.Latest(ctx, sid); err == nil {
			latest = &sample
		}
	}
	details := dto.FromServerDetails(srv, latest)
	if latest != nil {
		details.CPUSparkline, details.MemorySparkline, details.DiskSparkline, details.NetworkSparkline =
			s.sparklines(ctx, sid)
	}
	return details, nil
}

// TestConnection opens an SSH session and runs a trivial remote command.
func (s *Service) TestConnection(ctx context.Context, in dto.TestConnectionRequestDTO) (dto.TestConnectionResultDTO, error) {
	sid, err := server.ParseServerID(in.ServerID)
	if err != nil {
		return dto.TestConnectionResultDTO{}, shared.NewValidationError("server_id", "invalid server id")
	}
	srv, err := s.servers.Get(ctx, sid)
	if err != nil {
		return dto.TestConnectionResultDTO{}, err
	}
	if s.ssh == nil {
		return dto.TestConnectionResultDTO{
			Success:   false,
			Message:   "SSH factory not configured",
			ErrorCode: "ssh_unavailable",
		}, nil
	}

	start := time.Now()
	conn, err := s.ssh.Connect(ctx, srv)
	if err != nil {
		return dto.TestConnectionResultDTO{
			Success:   false,
			LatencyMs: time.Since(start).Milliseconds(),
			Message:   err.Error(),
			ErrorCode: classifySSHError(err),
		}, nil
	}
	defer func() { _ = conn.Close() }()

	res, err := conn.Execute(ctx, "echo hostdeck-ok && hostname")
	latency := time.Since(start).Milliseconds()
	if err != nil {
		return dto.TestConnectionResultDTO{
			Success:   false,
			LatencyMs: latency,
			Message:   err.Error(),
			ErrorCode: classifySSHError(err),
		}, nil
	}
	if res.ExitCode != 0 {
		return dto.TestConnectionResultDTO{
			Success:   false,
			LatencyMs: latency,
			Message:   strings.TrimSpace(res.Stderr),
			ErrorCode: "remote_command_failed",
		}, nil
	}
	return dto.TestConnectionResultDTO{
		Success:    true,
		LatencyMs:  latency,
		Message:    "connection successful",
		RemoteHost: strings.TrimSpace(res.Stdout),
	}, nil
}

func (s *Service) sparklines(ctx context.Context, id server.ServerID) (cpu, mem, disk, net []float64) {
	to := s.clock.Now()
	from := to.Add(-6 * time.Hour)
	history, err := s.metrics.History(ctx, id, from, to, 24)
	if err != nil || len(history) == 0 {
		return nil, nil, nil, nil
	}
	cpu = make([]float64, 0, len(history))
	mem = make([]float64, 0, len(history))
	disk = make([]float64, 0, len(history))
	net = make([]float64, 0, len(history))
	for _, sample := range history {
		cpu = append(cpu, sample.CPUTotalPercent)
		mem = append(mem, sample.MemoryUsedPercent())
		disk = append(disk, sample.DiskUsedPercent())
		net = append(net, sample.NetworkRXBytesPerSec+sample.NetworkTXBytesPerSec)
	}
	return cpu, mem, disk, net
}

func (s *Service) validateJumpHost(ctx context.Context, srv server.Server) error {
	if srv.ConnectionMode != server.ConnectionModeJump {
		return nil
	}
	if srv.JumpHostID == nil {
		return shared.NewValidationError("jump_host_id", "jump host is required for jump mode")
	}
	if *srv.JumpHostID == srv.ID {
		return shared.NewValidationError("jump_host_id", "server cannot jump through itself")
	}
	jump, err := s.servers.Get(ctx, *srv.JumpHostID)
	if err != nil {
		if err == shared.ErrNotFound {
			return shared.NewValidationError("jump_host_id", "jump host not found")
		}
		return err
	}
	// Cycle detection: walk jump chain.
	seen := map[server.ServerID]struct{}{srv.ID: {}}
	cur := jump
	for i := 0; i < 32; i++ {
		if _, ok := seen[cur.ID]; ok {
			return shared.NewValidationError("jump_host_id", "jump host cycle detected")
		}
		seen[cur.ID] = struct{}{}
		if cur.ConnectionMode != server.ConnectionModeJump || cur.JumpHostID == nil {
			return nil
		}
		next, err := s.servers.Get(ctx, *cur.JumpHostID)
		if err != nil {
			if err == shared.ErrNotFound {
				return shared.NewValidationError("jump_host_id", "jump host chain broken")
			}
			return err
		}
		cur = next
	}
	return shared.NewValidationError("jump_host_id", "jump host chain too deep")
}

func classifySSHError(err error) string {
	if err == nil {
		return ""
	}
	msg := strings.ToLower(err.Error())
	switch {
	case strings.Contains(msg, "auth"):
		return "authentication_failed"
	case strings.Contains(msg, "host key"):
		return "host_key_mismatch"
	case strings.Contains(msg, "timeout") || strings.Contains(msg, "deadline"):
		return "timeout"
	case strings.Contains(msg, "refused") || strings.Contains(msg, "unreachable"):
		return "unreachable"
	default:
		return "connection_failed"
	}
}
