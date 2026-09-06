// Package docker implements ContainerRuntime adapters for HostDeck V1.
package docker

import (
	"context"
	"encoding/json"
	"fmt"
	"strconv"
	"strings"
	"sync"
	"time"

	"github.com/alexandrebouttierdev/hostdeck/internal/application/ports"
	dockerdomain "github.com/alexandrebouttierdev/hostdeck/internal/domain/docker"
	"github.com/alexandrebouttierdev/hostdeck/internal/domain/server"
	"github.com/alexandrebouttierdev/hostdeck/internal/domain/shared"
)

// DemoRuntime is an in-memory container runtime with seeded demo containers.
type DemoRuntime struct {
	mu       sync.RWMutex
	byServer map[server.ServerID][]dockerdomain.Container
	hostInfo map[server.ServerID]dockerdomain.HostInfo
}

// NewDemoRuntime constructs a DemoRuntime with optional pre-seeded data.
func NewDemoRuntime() *DemoRuntime {
	r := &DemoRuntime{
		byServer: make(map[server.ServerID][]dockerdomain.Container),
		hostInfo: make(map[server.ServerID]dockerdomain.HostInfo),
	}
	r.seedDefaults()
	return r
}

// SeedServer registers demo containers for a server.
func (r *DemoRuntime) SeedServer(serverID server.ServerID, containers []dockerdomain.Container, info dockerdomain.HostInfo) {
	r.mu.Lock()
	defer r.mu.Unlock()
	r.byServer[serverID] = containers
	info.ServerID = serverID
	r.hostInfo[serverID] = info
}

func (r *DemoRuntime) seedDefaults() {
	// Empty until demo seed wires real server IDs.
}

func (r *DemoRuntime) Info(ctx context.Context, serverID server.ServerID) (dockerdomain.HostInfo, error) {
	_ = ctx
	r.mu.RLock()
	defer r.mu.RUnlock()
	info, ok := r.hostInfo[serverID]
	if !ok {
		return dockerdomain.HostInfo{
			ServerID:          serverID,
			DockerVersion:     "24.0.7",
			APIVersion:        "1.43",
			OS:                "linux",
			Architecture:      "x86_64",
			Containers:        len(r.byServer[serverID]),
			ContainersRunning: countRunning(r.byServer[serverID]),
			Images:            12,
			NCPU:              4,
			MemTotal:          8 << 30,
		}, nil
	}
	return info, nil
}

func (r *DemoRuntime) ListContainers(ctx context.Context, serverID server.ServerID) ([]dockerdomain.Container, error) {
	_ = ctx
	r.mu.RLock()
	defer r.mu.RUnlock()
	list := r.byServer[serverID]
	out := make([]dockerdomain.Container, len(list))
	copy(out, list)
	return out, nil
}

func (r *DemoRuntime) GetContainer(ctx context.Context, serverID server.ServerID, id dockerdomain.ContainerID) (dockerdomain.Container, error) {
	_ = ctx
	r.mu.RLock()
	defer r.mu.RUnlock()
	for _, c := range r.byServer[serverID] {
		if c.ID == id || strings.HasPrefix(c.ID.String(), id.String()) {
			return c, nil
		}
	}
	return dockerdomain.Container{}, shared.ErrNotFound
}

func (r *DemoRuntime) Start(ctx context.Context, serverID server.ServerID, id dockerdomain.ContainerID) error {
	return r.setStatus(serverID, id, dockerdomain.ContainerStatusRunning, "running")
}

func (r *DemoRuntime) Stop(ctx context.Context, serverID server.ServerID, id dockerdomain.ContainerID) error {
	return r.setStatus(serverID, id, dockerdomain.ContainerStatusExited, "exited")
}

func (r *DemoRuntime) Restart(ctx context.Context, serverID server.ServerID, id dockerdomain.ContainerID) error {
	r.mu.Lock()
	defer r.mu.Unlock()
	list := r.byServer[serverID]
	for i := range list {
		if list[i].ID == id || strings.HasPrefix(list[i].ID.String(), id.String()) {
			list[i].Status = dockerdomain.ContainerStatusRunning
			list[i].State = "running"
			list[i].RestartCount++
			now := time.Now().UTC()
			list[i].StartedAt = &now
			r.byServer[serverID] = list
			return nil
		}
	}
	return shared.ErrNotFound
}

func (r *DemoRuntime) Logs(ctx context.Context, serverID server.ServerID, id dockerdomain.ContainerID, tail int) ([]string, error) {
	_ = ctx
	if _, err := r.GetContainer(ctx, serverID, id); err != nil {
		return nil, err
	}
	if tail <= 0 {
		tail = 50
	}
	lines := make([]string, 0, tail)
	for i := 0; i < tail; i++ {
		lines = append(lines, fmt.Sprintf("[%s] demo log line %d for %s", time.Now().UTC().Format(time.RFC3339), i+1, id))
	}
	return lines, nil
}

func (r *DemoRuntime) setStatus(serverID server.ServerID, id dockerdomain.ContainerID, status dockerdomain.ContainerStatus, state string) error {
	r.mu.Lock()
	defer r.mu.Unlock()
	list := r.byServer[serverID]
	for i := range list {
		if list[i].ID == id || strings.HasPrefix(list[i].ID.String(), id.String()) {
			list[i].Status = status
			list[i].State = state
			if status == dockerdomain.ContainerStatusRunning {
				now := time.Now().UTC()
				list[i].StartedAt = &now
			}
			r.byServer[serverID] = list
			return nil
		}
	}
	return shared.ErrNotFound
}

func countRunning(list []dockerdomain.Container) int {
	n := 0
	for _, c := range list {
		if c.Status == dockerdomain.ContainerStatusRunning {
			n++
		}
	}
	return n
}

// SSHDockerRuntime executes docker CLI over SSH and parses JSON output.
type SSHDockerRuntime struct {
	factory ports.SSHConnectionFactory
	servers ports.ServerRepository
}

// NewSSHDockerRuntime constructs an SSH-backed Docker runtime.
func NewSSHDockerRuntime(factory ports.SSHConnectionFactory, servers ports.ServerRepository) *SSHDockerRuntime {
	return &SSHDockerRuntime{factory: factory, servers: servers}
}

func (r *SSHDockerRuntime) withConn(ctx context.Context, serverID server.ServerID, fn func(ports.SSHConnection) error) error {
	srv, err := r.servers.Get(ctx, serverID)
	if err != nil {
		return err
	}
	conn, err := r.factory.Connect(ctx, srv)
	if err != nil {
		return err
	}
	defer conn.Close()
	return fn(conn)
}

func (r *SSHDockerRuntime) Info(ctx context.Context, serverID server.ServerID) (dockerdomain.HostInfo, error) {
	var info dockerdomain.HostInfo
	err := r.withConn(ctx, serverID, func(conn ports.SSHConnection) error {
		res, err := conn.Execute(ctx, `docker info --format '{{json .}}'`)
		if err != nil {
			return err
		}
		if res.ExitCode != 0 {
			return fmt.Errorf("docker info: %s", strings.TrimSpace(res.Stderr))
		}
		var raw map[string]any
		if err := json.Unmarshal([]byte(res.Stdout), &raw); err != nil {
			return err
		}
		info = dockerdomain.HostInfo{
			ServerID:          serverID,
			DockerVersion:     asString(raw["ServerVersion"]),
			OS:                asString(raw["OperatingSystem"]),
			Architecture:      asString(raw["Architecture"]),
			Containers:        asInt(raw["Containers"]),
			ContainersRunning: asInt(raw["ContainersRunning"]),
			Images:            asInt(raw["Images"]),
			NCPU:              asInt(raw["NCPU"]),
			MemTotal:          asUint64(raw["MemTotal"]),
		}
		return nil
	})
	return info, err
}

func (r *SSHDockerRuntime) ListContainers(ctx context.Context, serverID server.ServerID) ([]dockerdomain.Container, error) {
	var out []dockerdomain.Container
	err := r.withConn(ctx, serverID, func(conn ports.SSHConnection) error {
		res, err := conn.Execute(ctx, `docker ps -a --format '{{json .}}'`)
		if err != nil {
			return err
		}
		if res.ExitCode != 0 {
			return fmt.Errorf("docker ps: %s", strings.TrimSpace(res.Stderr))
		}
		for _, line := range strings.Split(strings.TrimSpace(res.Stdout), "\n") {
			line = strings.TrimSpace(line)
			if line == "" {
				continue
			}
			var raw map[string]any
			if err := json.Unmarshal([]byte(line), &raw); err != nil {
				continue
			}
			out = append(out, parseDockerPS(serverID, raw))
		}
		return nil
	})
	return out, err
}

func (r *SSHDockerRuntime) GetContainer(ctx context.Context, serverID server.ServerID, id dockerdomain.ContainerID) (dockerdomain.Container, error) {
	list, err := r.ListContainers(ctx, serverID)
	if err != nil {
		return dockerdomain.Container{}, err
	}
	for _, c := range list {
		if c.ID == id || strings.HasPrefix(c.ID.String(), id.String()) {
			return c, nil
		}
	}
	return dockerdomain.Container{}, shared.ErrNotFound
}

func (r *SSHDockerRuntime) Start(ctx context.Context, serverID server.ServerID, id dockerdomain.ContainerID) error {
	return r.dockerCmd(ctx, serverID, "start", id.String())
}

func (r *SSHDockerRuntime) Stop(ctx context.Context, serverID server.ServerID, id dockerdomain.ContainerID) error {
	return r.dockerCmd(ctx, serverID, "stop", id.String())
}

func (r *SSHDockerRuntime) Restart(ctx context.Context, serverID server.ServerID, id dockerdomain.ContainerID) error {
	return r.dockerCmd(ctx, serverID, "restart", id.String())
}

func (r *SSHDockerRuntime) Logs(ctx context.Context, serverID server.ServerID, id dockerdomain.ContainerID, tail int) ([]string, error) {
	if tail <= 0 {
		tail = 100
	}
	var lines []string
	err := r.withConn(ctx, serverID, func(conn ports.SSHConnection) error {
		cmd := fmt.Sprintf("docker logs --tail %d %s", tail, shellQuote(id.String()))
		res, err := conn.Execute(ctx, cmd)
		if err != nil {
			return err
		}
		combined := res.Stdout
		if res.Stderr != "" {
			if combined != "" {
				combined += "\n"
			}
			combined += res.Stderr
		}
		for _, line := range strings.Split(combined, "\n") {
			if line != "" {
				lines = append(lines, line)
			}
		}
		return nil
	})
	return lines, err
}

func (r *SSHDockerRuntime) dockerCmd(ctx context.Context, serverID server.ServerID, action, id string) error {
	return r.withConn(ctx, serverID, func(conn ports.SSHConnection) error {
		res, err := conn.Execute(ctx, fmt.Sprintf("docker %s %s", action, shellQuote(id)))
		if err != nil {
			return err
		}
		if res.ExitCode != 0 {
			return fmt.Errorf("docker %s: %s", action, strings.TrimSpace(res.Stderr))
		}
		return nil
	})
}

func parseDockerPS(serverID server.ServerID, raw map[string]any) dockerdomain.Container {
	statusStr := asString(raw["Status"])
	state := asString(raw["State"])
	status := dockerdomain.ContainerStatusUnknown
	switch strings.ToLower(state) {
	case "running":
		status = dockerdomain.ContainerStatusRunning
	case "exited":
		status = dockerdomain.ContainerStatusExited
	case "paused":
		status = dockerdomain.ContainerStatusPaused
	case "restarting":
		status = dockerdomain.ContainerStatusRestarting
	case "created":
		status = dockerdomain.ContainerStatusCreated
	case "dead":
		status = dockerdomain.ContainerStatusDead
	}
	name := asString(raw["Names"])
	name = strings.TrimPrefix(name, "/")
	return dockerdomain.Container{
		ID:        dockerdomain.ContainerID(asString(raw["ID"])),
		ServerID:  serverID,
		Name:      name,
		Image:     asString(raw["Image"]),
		Status:    status,
		State:     firstNonEmpty(state, statusStr),
		Ports:     asString(raw["Ports"]),
		Networks:  asString(raw["Networks"]),
		Command:   asString(raw["Command"]),
		CreatedAt: time.Now().UTC(),
	}
}

func asString(v any) string {
	switch t := v.(type) {
	case string:
		return t
	case fmt.Stringer:
		return t.String()
	default:
		if v == nil {
			return ""
		}
		return fmt.Sprint(v)
	}
}

func asInt(v any) int {
	switch t := v.(type) {
	case float64:
		return int(t)
	case int:
		return t
	case string:
		n, _ := strconv.Atoi(t)
		return n
	default:
		return 0
	}
}

func asUint64(v any) uint64 {
	switch t := v.(type) {
	case float64:
		return uint64(t)
	case int:
		return uint64(t)
	case int64:
		return uint64(t)
	case string:
		n, _ := strconv.ParseUint(t, 10, 64)
		return n
	default:
		return 0
	}
}

func firstNonEmpty(a, b string) string {
	if a != "" {
		return a
	}
	return b
}

func shellQuote(s string) string {
	return "'" + strings.ReplaceAll(s, "'", `'"'"'`) + "'"
}

var _ ports.ContainerRuntime = (*DemoRuntime)(nil)
var _ ports.ContainerRuntime = (*SSHDockerRuntime)(nil)
