package dockerdomain

import (
	"time"

	"github.com/alexandrebouttierdev/hostdeck/internal/domain/server"
)

// ContainerStatus représente l'état d'un conteneur.
type ContainerStatus string

const (
	ContainerStatusRunning    ContainerStatus = "running"
	ContainerStatusExited     ContainerStatus = "exited"
	ContainerStatusPaused     ContainerStatus = "paused"
	ContainerStatusRestarting ContainerStatus = "restarting"
	ContainerStatusCreated    ContainerStatus = "created"
	ContainerStatusDead       ContainerStatus = "dead"
	ContainerStatusUnknown    ContainerStatus = "unknown"
)

// ContainerID identifie un conteneur Docker.
type ContainerID string

func (id ContainerID) String() string { return string(id) }

// Container représente un conteneur surveillé.
type Container struct {
	ID           ContainerID
	ServerID     server.ServerID
	Name         string
	Image        string
	Status       ContainerStatus
	State        string
	Ports        string
	Networks     string
	Command      string
	RestartCount int
	CPUPercent   float64
	MemoryUsed   uint64
	MemoryLimit  uint64
	NetworkRX    float64
	NetworkTX    float64
	BlockRead    float64
	BlockWrite   float64
	CreatedAt    time.Time
	StartedAt    *time.Time
}

func (c Container) MemoryPercent() float64 {
	if c.MemoryLimit == 0 {
		return 0
	}
	return float64(c.MemoryUsed) / float64(c.MemoryLimit) * 100
}

// HostInfo contient les infos Docker d'un hôte.
type HostInfo struct {
	ServerID         server.ServerID
	DockerVersion    string
	APIVersion       string
	OS               string
	Architecture     string
	Containers       int
	ContainersRunning int
	Images           int
	NCPU             int
	MemTotal         uint64
}
