package parsers

import (
	"errors"
	"testing"
)

func TestParseLoadAvg_Ubuntu2204Fixture(t *testing.T) {
	load, err := ParseLoadAvg(readFixture(t, "proc_loadavg"))
	if err != nil {
		t.Fatalf("ParseLoadAvg: %v", err)
	}
	if load.Load1 != 0.42 || load.Load5 != 0.58 || load.Load15 != 0.71 {
		t.Errorf("got %+v", load)
	}
}

func TestParseLoadAvg_Errors(t *testing.T) {
	if _, err := ParseLoadAvg(""); !errors.Is(err, ErrEmptyInput) {
		t.Errorf("empty: %v", err)
	}
	if _, err := ParseLoadAvg("0.1 0.2"); !errors.Is(err, ErrInvalidFormat) {
		t.Errorf("short: %v", err)
	}
	if _, err := ParseLoadAvg("x 0.2 0.3"); !errors.Is(err, ErrInvalidValue) {
		t.Errorf("invalid: %v", err)
	}
	if _, err := ParseLoadAvg("-1 0.2 0.3"); !errors.Is(err, ErrInvalidValue) {
		t.Errorf("negative: %v", err)
	}
}
