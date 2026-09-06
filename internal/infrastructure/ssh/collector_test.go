package ssh

import (
	"context"
	"os"
	"path/filepath"
	"runtime"
	"strings"
	"testing"
	"time"

	"github.com/alexandrebouttierdev/hostdeck/internal/application/ports"
)

type fakeSSH struct {
	responses map[string]ports.CommandResult
	calls     []string
}

func (f *fakeSSH) Execute(_ context.Context, command string) (ports.CommandResult, error) {
	f.calls = append(f.calls, command)
	if res, ok := f.responses[command]; ok {
		return res, nil
	}
	return ports.CommandResult{ExitCode: 1, Stderr: "unknown command"}, nil
}

func (f *fakeSSH) Close() error { return nil }

func loadUbuntuFixtures(t *testing.T) map[string]string {
	t.Helper()
	_, file, _, ok := runtime.Caller(0)
	if !ok {
		t.Fatal("runtime.Caller failed")
	}
	root := filepath.Clean(filepath.Join(filepath.Dir(file), "..", "..", ".."))
	dir := filepath.Join(root, "tests", "fixtures", "ubuntu2204")
	files := map[string]string{
		"cat /proc/stat":      "proc_stat",
		"cat /proc/meminfo":   "proc_meminfo",
		"cat /proc/loadavg":   "proc_loadavg",
		"cat /proc/net/dev":   "proc_net_dev",
		"df -B1":              "df_B1",
		"cat /proc/uptime":    "proc_uptime",
		"cat /etc/os-release": "os_release",
	}
	out := make(map[string]string, len(files))
	for cmd, name := range files {
		data, err := os.ReadFile(filepath.Join(dir, name))
		if err != nil {
			t.Fatalf("fixture %s: %v", name, err)
		}
		out[cmd] = string(data)
	}
	return out
}

func TestSystemCollector_Collect_FirstSampleCPUZero(t *testing.T) {
	fixtures := loadUbuntuFixtures(t)
	responses := make(map[string]ports.CommandResult, len(fixtures))
	for cmd, body := range fixtures {
		responses[cmd] = ports.CommandResult{Stdout: body, ExitCode: 0}
	}
	conn := &fakeSSH{responses: responses}
	c := NewSystemCollector(conn)

	sample, err := c.Collect(context.Background())
	if err != nil {
		t.Fatalf("Collect: %v", err)
	}
	if sample.CPUTotalPercent != 0 || sample.CPUUserPercent != 0 {
		t.Errorf("first sample CPU should be 0, got total=%f user=%f", sample.CPUTotalPercent, sample.CPUUserPercent)
	}
	if sample.MemoryTotalBytes == 0 {
		t.Error("expected memory total")
	}
	if sample.Load1 != 0.42 {
		t.Errorf("Load1 = %f", sample.Load1)
	}
	if sample.UptimeSeconds != 123456 {
		t.Errorf("UptimeSeconds = %d", sample.UptimeSeconds)
	}
	if sample.DiskTotalBytes == 0 || sample.DiskUsedBytes == 0 {
		t.Errorf("disk totals used=%d total=%d", sample.DiskUsedBytes, sample.DiskTotalBytes)
	}
	if sample.NetworkRXBytesPerSec != 0 {
		t.Errorf("first network rate should be 0, got %f", sample.NetworkRXBytesPerSec)
	}
}

func TestSystemCollector_Collect_SecondSampleCPUDelta(t *testing.T) {
	fixtures := loadUbuntuFixtures(t)
	responses := make(map[string]ports.CommandResult, len(fixtures))
	for cmd, body := range fixtures {
		responses[cmd] = ports.CommandResult{Stdout: body, ExitCode: 0}
	}
	// Mutate second /proc/stat to advance counters.
	firstStat := fixtures["cat /proc/stat"]
	secondStat := strings.Replace(firstStat,
		"cpu  238468 412 89432 1823456 5234 0 3120 890 0 0",
		"cpu  248468 512 91432 1833456 6234 10 4120 990 0 0",
		1,
	)
	conn := &fakeSSH{responses: responses}
	c := NewSystemCollector(conn)

	if _, err := c.Collect(context.Background()); err != nil {
		t.Fatal(err)
	}
	time.Sleep(10 * time.Millisecond)
	responses["cat /proc/stat"] = ports.CommandResult{Stdout: secondStat, ExitCode: 0}
	// Also bump network counters.
	secondNet := strings.Replace(fixtures["cat /proc/net/dev"],
		"eth0: 9876543210  5000000    0    2    0     0          0      1000 5432109876  4000000    0    0    0     0       0          0",
		"eth0: 9876643210  5001000    0    2    0     0          0      1000 5432209876  4001000    0    0    0     0       0          0",
		1,
	)
	responses["cat /proc/net/dev"] = ports.CommandResult{Stdout: secondNet, ExitCode: 0}

	sample, err := c.Collect(context.Background())
	if err != nil {
		t.Fatal(err)
	}
	if sample.CPUTotalPercent <= 0 {
		t.Errorf("expected positive CPU total on second sample, got %f", sample.CPUTotalPercent)
	}
	if sample.NetworkRXBytesPerSec <= 0 {
		t.Errorf("expected positive RX rate, got %f", sample.NetworkRXBytesPerSec)
	}
}

func TestSystemCollector_Collect_CommandFailure(t *testing.T) {
	conn := &fakeSSH{responses: map[string]ports.CommandResult{
		"cat /proc/stat": {ExitCode: 1, Stderr: "permission denied"},
	}}
	c := NewSystemCollector(conn)
	if _, err := c.Collect(context.Background()); err == nil {
		t.Fatal("expected error")
	}
}
