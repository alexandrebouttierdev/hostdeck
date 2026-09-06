package parsers

import (
	"bufio"
	"strconv"
	"strings"
)

// CPUStat holds cumulative CPU time counters from the aggregate "cpu" line in /proc/stat.
// Units are USER_HZ (typically 1/100 s).
type CPUStat struct {
	User    uint64
	Nice    uint64
	System  uint64
	Idle    uint64
	IOWait  uint64
	IRQ     uint64
	SoftIRQ uint64
	Steal   uint64
}

// Total returns the sum of all tracked counters.
func (s CPUStat) Total() uint64 {
	return s.User + s.Nice + s.System + s.Idle + s.IOWait + s.IRQ + s.SoftIRQ + s.Steal
}

// CPUUsagePercent holds CPU usage percentages derived from two samples.
type CPUUsagePercent struct {
	User   float64
	System float64
	IOWait float64
	Nice   float64
	Steal  float64
	Total  float64
}

// ParseCPUStat parses /proc/stat and returns counters from the first aggregate "cpu" line.
func ParseCPUStat(input string) (CPUStat, error) {
	const op = "ParseCPUStat"
	if strings.TrimSpace(input) == "" {
		return CPUStat{}, newParseError(op, "", ErrEmptyInput)
	}

	scanner := bufio.NewScanner(strings.NewReader(input))
	for scanner.Scan() {
		line := strings.TrimSpace(scanner.Text())
		if line == "" {
			continue
		}
		fields := strings.Fields(line)
		if len(fields) == 0 || fields[0] != "cpu" {
			continue
		}
		// Need at least user, nice, system, idle (classic minimum).
		if len(fields) < 5 {
			return CPUStat{}, newParseError(op, "cpu", ErrInvalidFormat)
		}

		values := make([]uint64, 8)
		for i := 0; i < 8; i++ {
			idx := i + 1
			if idx >= len(fields) {
				break
			}
			v, err := strconv.ParseUint(fields[idx], 10, 64)
			if err != nil {
				return CPUStat{}, newParseError(op, fieldName(i), ErrInvalidValue)
			}
			values[i] = v
		}

		return CPUStat{
			User:    values[0],
			Nice:    values[1],
			System:  values[2],
			Idle:    values[3],
			IOWait:  values[4],
			IRQ:     values[5],
			SoftIRQ: values[6],
			Steal:   values[7],
		}, nil
	}

	if err := scanner.Err(); err != nil {
		return CPUStat{}, newParseError(op, "", err)
	}
	return CPUStat{}, newParseError(op, "cpu", ErrMissingField)
}

func fieldName(i int) string {
	names := []string{"user", "nice", "system", "idle", "iowait", "irq", "softirq", "steal"}
	if i >= 0 && i < len(names) {
		return names[i]
	}
	return "field"
}

// ComputeCPUUsagePercent computes usage percentages between two /proc/stat samples.
// When total delta is zero (or curr is not ahead of prev), all percentages are 0.
func ComputeCPUUsagePercent(prev, curr CPUStat) CPUUsagePercent {
	dUser := delta(curr.User, prev.User)
	dNice := delta(curr.Nice, prev.Nice)
	dSystem := delta(curr.System, prev.System)
	dIdle := delta(curr.Idle, prev.Idle)
	dIOWait := delta(curr.IOWait, prev.IOWait)
	dIRQ := delta(curr.IRQ, prev.IRQ)
	dSoftIRQ := delta(curr.SoftIRQ, prev.SoftIRQ)
	dSteal := delta(curr.Steal, prev.Steal)

	total := dUser + dNice + dSystem + dIdle + dIOWait + dIRQ + dSoftIRQ + dSteal
	if total == 0 {
		return CPUUsagePercent{}
	}

	ft := float64(total)
	user := float64(dUser) / ft * 100
	system := float64(dSystem) / ft * 100
	iowait := float64(dIOWait) / ft * 100
	nice := float64(dNice) / ft * 100
	steal := float64(dSteal) / ft * 100
	busy := float64(dUser+dNice+dSystem+dIOWait+dIRQ+dSoftIRQ+dSteal) / ft * 100

	return CPUUsagePercent{
		User:   user,
		System: system,
		IOWait: iowait,
		Nice:   nice,
		Steal:  steal,
		Total:  busy,
	}
}

func delta(curr, prev uint64) uint64 {
	if curr < prev {
		return 0
	}
	return curr - prev
}
