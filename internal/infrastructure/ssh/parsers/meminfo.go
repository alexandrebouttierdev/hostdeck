package parsers

import (
	"bufio"
	"strconv"
	"strings"
)

const kibibyte = 1024

// MemInfo holds memory statistics from /proc/meminfo, converted to bytes.
type MemInfo struct {
	MemTotal     uint64
	MemFree      uint64
	MemAvailable uint64
	Buffers      uint64
	Cached       uint64
	SwapTotal    uint64
	SwapFree     uint64
}

// ParseMemInfo parses /proc/meminfo. Values in the file are in kB and are converted to bytes.
func ParseMemInfo(input string) (MemInfo, error) {
	const op = "ParseMemInfo"
	if strings.TrimSpace(input) == "" {
		return MemInfo{}, newParseError(op, "", ErrEmptyInput)
	}

	required := map[string]*uint64{}
	var info MemInfo
	required["MemTotal"] = &info.MemTotal
	required["MemFree"] = &info.MemFree
	required["MemAvailable"] = &info.MemAvailable
	required["Buffers"] = &info.Buffers
	required["Cached"] = &info.Cached
	required["SwapTotal"] = &info.SwapTotal
	required["SwapFree"] = &info.SwapFree

	found := make(map[string]bool, len(required))

	scanner := bufio.NewScanner(strings.NewReader(input))
	for scanner.Scan() {
		line := strings.TrimSpace(scanner.Text())
		if line == "" {
			continue
		}
		// Format: Key: value kB
		colon := strings.IndexByte(line, ':')
		if colon < 0 {
			continue
		}
		key := strings.TrimSpace(line[:colon])
		ptr, ok := required[key]
		if !ok {
			continue
		}
		rest := strings.TrimSpace(line[colon+1:])
		fields := strings.Fields(rest)
		if len(fields) == 0 {
			return MemInfo{}, newParseError(op, key, ErrInvalidValue)
		}
		kb, err := strconv.ParseUint(fields[0], 10, 64)
		if err != nil {
			return MemInfo{}, newParseError(op, key, ErrInvalidValue)
		}
		*ptr = kb * kibibyte
		found[key] = true
	}
	if err := scanner.Err(); err != nil {
		return MemInfo{}, newParseError(op, "", err)
	}

	for key := range required {
		if !found[key] {
			return MemInfo{}, newParseError(op, key, ErrMissingField)
		}
	}
	return info, nil
}

// MemoryUsedBytes returns approximate used memory: MemTotal - MemAvailable.
func (m MemInfo) MemoryUsedBytes() uint64 {
	if m.MemAvailable >= m.MemTotal {
		return 0
	}
	return m.MemTotal - m.MemAvailable
}

// SwapUsedBytes returns SwapTotal - SwapFree.
func (m MemInfo) SwapUsedBytes() uint64 {
	if m.SwapFree >= m.SwapTotal {
		return 0
	}
	return m.SwapTotal - m.SwapFree
}
