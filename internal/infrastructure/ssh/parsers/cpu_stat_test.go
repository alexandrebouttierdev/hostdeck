package parsers

import (
	"errors"
	"math"
	"testing"
)

func TestParseCPUStat_Ubuntu2204Fixture(t *testing.T) {
	stat, err := ParseCPUStat(readFixture(t, "proc_stat"))
	if err != nil {
		t.Fatalf("ParseCPUStat: %v", err)
	}
	if stat.User != 238468 {
		t.Errorf("User = %d, want 238468", stat.User)
	}
	if stat.Nice != 412 {
		t.Errorf("Nice = %d, want 412", stat.Nice)
	}
	if stat.System != 89432 {
		t.Errorf("System = %d, want 89432", stat.System)
	}
	if stat.Idle != 1823456 {
		t.Errorf("Idle = %d, want 1823456", stat.Idle)
	}
	if stat.IOWait != 5234 {
		t.Errorf("IOWait = %d, want 5234", stat.IOWait)
	}
	if stat.IRQ != 0 {
		t.Errorf("IRQ = %d, want 0", stat.IRQ)
	}
	if stat.SoftIRQ != 3120 {
		t.Errorf("SoftIRQ = %d, want 3120", stat.SoftIRQ)
	}
	if stat.Steal != 890 {
		t.Errorf("Steal = %d, want 890", stat.Steal)
	}
}

func TestParseCPUStat_EmptyAndMissing(t *testing.T) {
	if _, err := ParseCPUStat(""); !errors.Is(err, ErrEmptyInput) {
		t.Errorf("empty: err = %v, want ErrEmptyInput", err)
	}
	if _, err := ParseCPUStat("intr 1 2 3\nctxt 9\n"); !errors.Is(err, ErrMissingField) {
		t.Errorf("missing cpu: err = %v, want ErrMissingField", err)
	}
	if _, err := ParseCPUStat("cpu 1 2\n"); !errors.Is(err, ErrInvalidFormat) {
		t.Errorf("short line: err = %v, want ErrInvalidFormat", err)
	}
	if _, err := ParseCPUStat("cpu abc 0 0 0\n"); !errors.Is(err, ErrInvalidValue) {
		t.Errorf("invalid value: err = %v, want ErrInvalidValue", err)
	}
}

func TestComputeCPUUsagePercent(t *testing.T) {
	prev := CPUStat{User: 100, Nice: 10, System: 50, Idle: 800, IOWait: 20, IRQ: 5, SoftIRQ: 5, Steal: 10}
	curr := CPUStat{User: 150, Nice: 20, System: 70, Idle: 900, IOWait: 40, IRQ: 10, SoftIRQ: 10, Steal: 20}
	// deltas: user50 nice10 system20 idle100 iowait20 irq5 softirq5 steal10 = 220
	usage := ComputeCPUUsagePercent(prev, curr)
	approx := func(got, want float64) {
		t.Helper()
		if math.Abs(got-want) > 0.01 {
			t.Errorf("got %f want %f", got, want)
		}
	}
	approx(usage.User, 50.0/220*100)
	approx(usage.Nice, 10.0/220*100)
	approx(usage.System, 20.0/220*100)
	approx(usage.IOWait, 20.0/220*100)
	approx(usage.Steal, 10.0/220*100)
	approx(usage.Total, 120.0/220*100)

	if zero := ComputeCPUUsagePercent(curr, curr); zero != (CPUUsagePercent{}) {
		t.Errorf("zero delta: %+v", zero)
	}
	if zero := ComputeCPUUsagePercent(curr, prev); zero.Total != 0 {
		t.Errorf("counter wrap/backward: %+v", zero)
	}
}
