package monitoring

import (
	"strings"

	"github.com/alexandrebouttierdev/hostdeck/internal/domain/incident"
	domainmon "github.com/alexandrebouttierdev/hostdeck/internal/domain/monitoring"
)

// MetricValue extracts a named metric from a sample.
// Supported: cpu_total, memory_used_percent, disk_used_percent, load1, load5, load15, swap_used_percent.
func MetricValue(sample domainmon.MetricSample, metric string) (float64, bool) {
	switch strings.ToLower(strings.TrimSpace(metric)) {
	case "cpu", "cpu_total", "cpu_total_percent":
		return sample.CPUTotalPercent, true
	case "memory", "memory_used_percent", "mem":
		return sample.MemoryUsedPercent(), true
	case "disk", "disk_used_percent":
		return sample.DiskUsedPercent(), true
	case "load", "load1":
		return sample.Load1, true
	case "load5":
		return sample.Load5, true
	case "load15":
		return sample.Load15, true
	case "swap", "swap_used_percent":
		return sample.SwapUsedPercent(), true
	case "availability", "online":
		// Presence of a collected sample implies the host was reachable.
		return 1, true
	default:
		return 0, false
	}
}

// EvaluateRule reports whether the sample breaches the alert rule threshold.
func EvaluateRule(sample domainmon.MetricSample, rule incident.AlertRule) bool {
	if !rule.Enabled {
		return false
	}
	value, ok := MetricValue(sample, rule.Metric)
	if !ok {
		return false
	}
	return compare(value, rule.Operator, rule.Threshold)
}

func compare(value float64, operator string, threshold float64) bool {
	switch strings.TrimSpace(operator) {
	case ">", "gt":
		return value > threshold
	case ">=", "gte":
		return value >= threshold
	case "<", "lt":
		return value < threshold
	case "<=", "lte":
		return value <= threshold
	case "==", "=", "eq":
		return value == threshold
	case "!=", "<>", "ne":
		return value != threshold
	default:
		return value > threshold
	}
}
