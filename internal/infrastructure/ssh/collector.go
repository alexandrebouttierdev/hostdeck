package ssh

import (
	"context"
	"fmt"
	"strings"
	"time"

	"github.com/alexandrebouttierdev/hostdeck/internal/application/ports"
	"github.com/alexandrebouttierdev/hostdeck/internal/domain/monitoring"
	"github.com/alexandrebouttierdev/hostdeck/internal/infrastructure/ssh/parsers"
)

// SystemCollector collects Linux host metrics over an SSH connection.
type SystemCollector struct {
	conn ports.SSHConnection

	prevCPU    parsers.CPUStat
	hasPrevCPU bool
	prevNet    map[string]parsers.NetDevCounters
	prevNetAt  time.Time
}

// NewSystemCollector creates a collector bound to the given SSH connection.
func NewSystemCollector(conn ports.SSHConnection) *SystemCollector {
	return &SystemCollector{conn: conn}
}

// Bind replaces the underlying SSH connection while preserving delta state
// (previous CPU / network counters) across collection rounds.
func (c *SystemCollector) Bind(conn ports.SSHConnection) {
	c.conn = conn
}

// Collect runs remote commands, parses their output, and returns a MetricSample.
// On the first call, CPU percentages are 0 (no previous sample for delta).
// Network rates are 0 until a previous sample exists.
func (c *SystemCollector) Collect(ctx context.Context) (monitoring.MetricSample, error) {
	now := time.Now().UTC()
	sample := monitoring.MetricSample{CollectedAt: now}

	statOut, err := c.run(ctx, "cat /proc/stat")
	if err != nil {
		return sample, fmt.Errorf("collect cpu: %w", err)
	}
	cpu, err := parsers.ParseCPUStat(statOut)
	if err != nil {
		return sample, fmt.Errorf("collect cpu: %w", err)
	}
	if c.hasPrevCPU {
		usage := parsers.ComputeCPUUsagePercent(c.prevCPU, cpu)
		sample.CPUUserPercent = usage.User
		sample.CPUSystemPercent = usage.System
		sample.CPUIOWaitPercent = usage.IOWait
		sample.CPUNicePercent = usage.Nice
		sample.CPUStealPercent = usage.Steal
		sample.CPUTotalPercent = usage.Total
	}
	c.prevCPU = cpu
	c.hasPrevCPU = true

	memOut, err := c.run(ctx, "cat /proc/meminfo")
	if err != nil {
		return sample, fmt.Errorf("collect meminfo: %w", err)
	}
	mem, err := parsers.ParseMemInfo(memOut)
	if err != nil {
		return sample, fmt.Errorf("collect meminfo: %w", err)
	}
	sample.MemoryTotalBytes = mem.MemTotal
	sample.MemoryFreeBytes = mem.MemFree
	sample.MemoryCacheBytes = mem.Cached
	sample.MemoryBufferBytes = mem.Buffers
	sample.MemoryUsedBytes = mem.MemoryUsedBytes()
	sample.SwapTotalBytes = mem.SwapTotal
	sample.SwapUsedBytes = mem.SwapUsedBytes()

	loadOut, err := c.run(ctx, "cat /proc/loadavg")
	if err != nil {
		return sample, fmt.Errorf("collect loadavg: %w", err)
	}
	load, err := parsers.ParseLoadAvg(loadOut)
	if err != nil {
		return sample, fmt.Errorf("collect loadavg: %w", err)
	}
	sample.Load1 = load.Load1
	sample.Load5 = load.Load5
	sample.Load15 = load.Load15

	netOut, err := c.run(ctx, "cat /proc/net/dev")
	if err != nil {
		return sample, fmt.Errorf("collect netdev: %w", err)
	}
	net, err := parsers.ParseNetworkDev(netOut)
	if err != nil {
		return sample, fmt.Errorf("collect netdev: %w", err)
	}
	var rxTotal, txTotal uint64
	for _, ctr := range net {
		rxTotal += ctr.RXBytes
		txTotal += ctr.TXBytes
	}
	if c.prevNet != nil && !c.prevNetAt.IsZero() {
		elapsed := now.Sub(c.prevNetAt).Seconds()
		if elapsed > 0 {
			var prevRX, prevTX uint64
			for _, ctr := range c.prevNet {
				prevRX += ctr.RXBytes
				prevTX += ctr.TXBytes
			}
			if rxTotal >= prevRX {
				sample.NetworkRXBytesPerSec = float64(rxTotal-prevRX) / elapsed
			}
			if txTotal >= prevTX {
				sample.NetworkTXBytesPerSec = float64(txTotal-prevTX) / elapsed
			}
		}
	}
	c.prevNet = net
	c.prevNetAt = now

	dfOut, err := c.run(ctx, "df -B1")
	if err != nil {
		return sample, fmt.Errorf("collect disk: %w", err)
	}
	disks, err := parsers.ParseDiskUsage(dfOut)
	if err != nil {
		return sample, fmt.Errorf("collect disk: %w", err)
	}
	for _, d := range disks {
		sample.DiskUsedBytes += d.UsedBytes
		sample.DiskTotalBytes += d.TotalBytes
	}

	uptimeOut, err := c.run(ctx, "cat /proc/uptime")
	if err != nil {
		return sample, fmt.Errorf("collect uptime: %w", err)
	}
	uptime, err := parsers.ParseUptime(uptimeOut)
	if err != nil {
		return sample, fmt.Errorf("collect uptime: %w", err)
	}
	sample.UptimeSeconds = uptime

	// OS release is collected for side use / future enrichment; ignore parse for sample fields.
	if osOut, err := c.run(ctx, "cat /etc/os-release"); err == nil {
		_, _ = parsers.ParseOSRelease(osOut)
	}

	return sample, nil
}

func (c *SystemCollector) run(ctx context.Context, command string) (string, error) {
	if c.conn == nil {
		return "", fmt.Errorf("ssh connection is nil")
	}
	res, err := c.conn.Execute(ctx, command)
	if err != nil {
		return "", err
	}
	if res.ExitCode != 0 {
		msg := strings.TrimSpace(res.Stderr)
		if msg == "" {
			msg = strings.TrimSpace(res.Stdout)
		}
		if msg == "" {
			msg = "non-zero exit"
		}
		return "", fmt.Errorf("%s: exit %d: %s", command, res.ExitCode, msg)
	}
	return res.Stdout, nil
}
