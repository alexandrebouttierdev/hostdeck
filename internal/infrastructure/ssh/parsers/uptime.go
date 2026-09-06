package parsers

import (
	"math"
	"strconv"
	"strings"
)

// ParseUptime parses /proc/uptime and returns the first value as whole seconds.
func ParseUptime(input string) (uint64, error) {
	const op = "ParseUptime"
	line := strings.TrimSpace(input)
	if line == "" {
		return 0, newParseError(op, "", ErrEmptyInput)
	}
	if i := strings.IndexByte(line, '\n'); i >= 0 {
		line = strings.TrimSpace(line[:i])
	}
	fields := strings.Fields(line)
	if len(fields) < 1 {
		return 0, newParseError(op, "", ErrInvalidFormat)
	}
	secs, err := strconv.ParseFloat(fields[0], 64)
	if err != nil || math.IsNaN(secs) || math.IsInf(secs, 0) || secs < 0 {
		return 0, newParseError(op, "uptime", ErrInvalidValue)
	}
	return uint64(secs), nil
}
