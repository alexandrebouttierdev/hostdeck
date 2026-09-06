package parsers

import (
	"errors"
	"testing"
)

func TestParseMemInfo_Ubuntu2204Fixture(t *testing.T) {
	info, err := ParseMemInfo(readFixture(t, "proc_meminfo"))
	if err != nil {
		t.Fatalf("ParseMemInfo: %v", err)
	}
	want := func(name string, got, kb uint64) {
		t.Helper()
		if got != kb*1024 {
			t.Errorf("%s = %d, want %d", name, got, kb*1024)
		}
	}
	want("MemTotal", info.MemTotal, 16384000)
	want("MemFree", info.MemFree, 2048000)
	want("MemAvailable", info.MemAvailable, 8192000)
	want("Buffers", info.Buffers, 512000)
	want("Cached", info.Cached, 4096000)
	want("SwapTotal", info.SwapTotal, 2097152)
	want("SwapFree", info.SwapFree, 1572864)

	if info.MemoryUsedBytes() != (16384000-8192000)*1024 {
		t.Errorf("MemoryUsedBytes = %d", info.MemoryUsedBytes())
	}
	if info.SwapUsedBytes() != (2097152-1572864)*1024 {
		t.Errorf("SwapUsedBytes = %d", info.SwapUsedBytes())
	}
}

func TestParseMemInfo_Errors(t *testing.T) {
	if _, err := ParseMemInfo(""); !errors.Is(err, ErrEmptyInput) {
		t.Errorf("empty: %v", err)
	}
	partial := "MemTotal: 1000 kB\nMemFree: 100 kB\n"
	if _, err := ParseMemInfo(partial); !errors.Is(err, ErrMissingField) {
		t.Errorf("missing fields: %v", err)
	}
	bad := "MemTotal: abc kB\nMemFree: 1 kB\nMemAvailable: 1 kB\nBuffers: 1 kB\nCached: 1 kB\nSwapTotal: 1 kB\nSwapFree: 1 kB\n"
	if _, err := ParseMemInfo(bad); !errors.Is(err, ErrInvalidValue) {
		t.Errorf("invalid value: %v", err)
	}
}
