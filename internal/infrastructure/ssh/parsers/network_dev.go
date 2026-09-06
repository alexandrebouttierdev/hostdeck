package parsers

import (
	"bufio"
	"strconv"
	"strings"
)

// NetDevCounters holds cumulative RX/TX byte counters for a network interface.
type NetDevCounters struct {
	RXBytes uint64
	TXBytes uint64
}

// ParseNetworkDev parses /proc/net/dev and returns counters for each interface,
// skipping the loopback interface "lo".
func ParseNetworkDev(input string) (map[string]NetDevCounters, error) {
	const op = "ParseNetworkDev"
	if strings.TrimSpace(input) == "" {
		return nil, newParseError(op, "", ErrEmptyInput)
	}

	result := make(map[string]NetDevCounters)
	scanner := bufio.NewScanner(strings.NewReader(input))
	lineNo := 0
	for scanner.Scan() {
		lineNo++
		line := strings.TrimSpace(scanner.Text())
		if line == "" {
			continue
		}
		// First two lines are headers.
		if lineNo <= 2 && (strings.Contains(line, "|") || strings.HasPrefix(line, "Inter-") || strings.HasPrefix(line, "face")) {
			continue
		}
		// Also skip header-like lines that appear without counting.
		if strings.HasPrefix(line, "Inter-") || (strings.HasPrefix(line, "face") && strings.Contains(line, "Receive")) {
			continue
		}

		colon := strings.IndexByte(line, ':')
		if colon < 0 {
			continue
		}
		iface := strings.TrimSpace(line[:colon])
		if iface == "" || iface == "lo" {
			continue
		}
		rest := strings.TrimSpace(line[colon+1:])
		fields := strings.Fields(rest)
		// Need at least RX bytes (0) and TX bytes (8).
		if len(fields) < 9 {
			return nil, newParseError(op, iface, ErrInvalidFormat)
		}
		rx, err := strconv.ParseUint(fields[0], 10, 64)
		if err != nil {
			return nil, newParseError(op, iface+".rx_bytes", ErrInvalidValue)
		}
		tx, err := strconv.ParseUint(fields[8], 10, 64)
		if err != nil {
			return nil, newParseError(op, iface+".tx_bytes", ErrInvalidValue)
		}
		result[iface] = NetDevCounters{RXBytes: rx, TXBytes: tx}
	}
	if err := scanner.Err(); err != nil {
		return nil, newParseError(op, "", err)
	}
	return result, nil
}
