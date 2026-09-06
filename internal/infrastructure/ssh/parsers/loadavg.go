package parsers

import (
	"strconv"
	"strings"
)

// LoadAvg holds the 1-, 5-, and 15-minute load averages from /proc/loadavg.
type LoadAvg struct {
	Load1  float64
	Load5  float64
	Load15 float64
}

// ParseLoadAvg parses /proc/loadavg.
func ParseLoadAvg(input string) (LoadAvg, error) {
	const op = "ParseLoadAvg"
	line := strings.TrimSpace(input)
	if line == "" {
		return LoadAvg{}, newParseError(op, "", ErrEmptyInput)
	}
	// Only the first line matters.
	if i := strings.IndexByte(line, '\n'); i >= 0 {
		line = strings.TrimSpace(line[:i])
	}
	fields := strings.Fields(line)
	if len(fields) < 3 {
		return LoadAvg{}, newParseError(op, "", ErrInvalidFormat)
	}

	names := []string{"load1", "load5", "load15"}
	vals := make([]float64, 3)
	for i := 0; i < 3; i++ {
		v, err := strconv.ParseFloat(fields[i], 64)
		if err != nil {
			return LoadAvg{}, newParseError(op, names[i], ErrInvalidValue)
		}
		if v < 0 {
			return LoadAvg{}, newParseError(op, names[i], ErrInvalidValue)
		}
		vals[i] = v
	}
	return LoadAvg{Load1: vals[0], Load5: vals[1], Load15: vals[2]}, nil
}
