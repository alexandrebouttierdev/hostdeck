package parsers

import (
	"errors"
	"testing"
)

func TestParseUptime_Ubuntu2204Fixture(t *testing.T) {
	secs, err := ParseUptime(readFixture(t, "proc_uptime"))
	if err != nil {
		t.Fatalf("ParseUptime: %v", err)
	}
	if secs != 123456 {
		t.Errorf("uptime = %d, want 123456", secs)
	}
}

func TestParseUptime_Errors(t *testing.T) {
	if _, err := ParseUptime(""); !errors.Is(err, ErrEmptyInput) {
		t.Errorf("empty: %v", err)
	}
	if _, err := ParseUptime("not-a-number 1"); !errors.Is(err, ErrInvalidValue) {
		t.Errorf("invalid: %v", err)
	}
	if _, err := ParseUptime("-5.0 1.0"); !errors.Is(err, ErrInvalidValue) {
		t.Errorf("negative: %v", err)
	}
}
