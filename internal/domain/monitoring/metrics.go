package monitoring

import (
	"time"

	"github.com/alexandrebouttierdev/hostdeck/internal/domain/server"
)

// MetricSample contient un échantillon de métriques système.
type MetricSample struct {
	ID          string
	ServerID    server.ServerID
	CollectedAt time.Time

	CPUUserPercent   float64
	CPUSystemPercent float64
	CPUIOWaitPercent float64
	CPUNicePercent   float64
	CPUStealPercent  float64
	CPUTotalPercent  float64

	MemoryUsedBytes   uint64
	MemoryCacheBytes  uint64
	MemoryBufferBytes uint64
	MemoryFreeBytes   uint64
	MemoryTotalBytes  uint64
	SwapUsedBytes     uint64
	SwapTotalBytes    uint64

	Load1  float64
	Load5  float64
	Load15 float64

	NetworkRXBytesPerSec float64
	NetworkTXBytesPerSec float64

	DiskUsedBytes        uint64
	DiskTotalBytes       uint64
	DiskReadBytesPerSec  float64
	DiskWriteBytesPerSec float64

	UptimeSeconds uint64
}

func (m MetricSample) MemoryUsedPercent() float64 {
	if m.MemoryTotalBytes == 0 {
		return 0
	}
	return float64(m.MemoryUsedBytes) / float64(m.MemoryTotalBytes) * 100
}

func (m MetricSample) DiskUsedPercent() float64 {
	if m.DiskTotalBytes == 0 {
		return 0
	}
	return float64(m.DiskUsedBytes) / float64(m.DiskTotalBytes) * 100
}

func (m MetricSample) SwapUsedPercent() float64 {
	if m.SwapTotalBytes == 0 {
		return 0
	}
	return float64(m.SwapUsedBytes) / float64(m.SwapTotalBytes) * 100
}

// DiskMetricSample décrit l'usage d'un volume.
type DiskMetricSample struct {
	ID          string
	ServerID    server.ServerID
	CollectedAt time.Time
	MountPoint  string
	Device      string
	UsedBytes   uint64
	TotalBytes  uint64
	UsedPercent float64
}

// NetworkMetricSample décrit le trafic d'une interface.
type NetworkMetricSample struct {
	ID            string
	ServerID      server.ServerID
	CollectedAt   time.Time
	Interface     string
	RXBytesPerSec float64
	TXBytesPerSec float64
}

// TimeRange représente une plage d'historique.
type TimeRange string

const (
	TimeRange15Min TimeRange = "15m"
	TimeRange1H    TimeRange = "1h"
	TimeRange2H    TimeRange = "2h"
	TimeRange6H    TimeRange = "6h"
	TimeRange12H   TimeRange = "12h"
	TimeRange24H   TimeRange = "24h"
	TimeRange7D    TimeRange = "7d"
	TimeRange30D   TimeRange = "30d"
)

func (r TimeRange) Duration() time.Duration {
	switch r {
	case TimeRange15Min:
		return 15 * time.Minute
	case TimeRange1H:
		return time.Hour
	case TimeRange2H:
		return 2 * time.Hour
	case TimeRange6H:
		return 6 * time.Hour
	case TimeRange12H:
		return 12 * time.Hour
	case TimeRange24H:
		return 24 * time.Hour
	case TimeRange7D:
		return 7 * 24 * time.Hour
	case TimeRange30D:
		return 30 * 24 * time.Hour
	default:
		return 6 * time.Hour
	}
}
