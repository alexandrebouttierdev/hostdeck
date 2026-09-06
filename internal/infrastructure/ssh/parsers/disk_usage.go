package parsers

import (
	"bufio"
	"strconv"
	"strings"
)

// DiskUsage describes a single mounted filesystem from `df -B1`.
type DiskUsage struct {
	Device     string
	MountPoint string
	UsedBytes  uint64
	TotalBytes uint64
}

var skipFSTypes = map[string]bool{
	"tmpfs":     true,
	"devtmpfs":  true,
	"squashfs":  true,
}

// ParseDiskUsage parses POSIX-style `df -B1` output.
// Entries whose filesystem type or device name is tmpfs/devtmpfs/squashfs are skipped.
// When Type is present (df -BT), it is used; otherwise device names matching those
// types (literal device column equal to the type name) are skipped.
func ParseDiskUsage(input string) ([]DiskUsage, error) {
	const op = "ParseDiskUsage"
	if strings.TrimSpace(input) == "" {
		return nil, newParseError(op, "", ErrEmptyInput)
	}

	scanner := bufio.NewScanner(strings.NewReader(input))
	var results []DiskUsage
	headerSeen := false
	hasType := false

	for scanner.Scan() {
		line := strings.TrimSpace(scanner.Text())
		if line == "" {
			continue
		}
		fields := strings.Fields(line)
		if len(fields) == 0 {
			continue
		}

		// Header detection.
		if !headerSeen && (fields[0] == "Filesystem" || strings.EqualFold(fields[0], "Filesystem")) {
			headerSeen = true
			for _, f := range fields {
				if f == "Type" {
					hasType = true
					break
				}
			}
			continue
		}

		var device, fsType, mount string
		var total, used uint64
		var err error

		if hasType {
			// Filesystem Type 1B-blocks Used Available Use% Mounted on
			if len(fields) < 7 {
				return nil, newParseError(op, fields[0], ErrInvalidFormat)
			}
			device = fields[0]
			fsType = fields[1]
			total, err = strconv.ParseUint(fields[2], 10, 64)
			if err != nil {
				return nil, newParseError(op, device+".total", ErrInvalidValue)
			}
			used, err = strconv.ParseUint(fields[3], 10, 64)
			if err != nil {
				return nil, newParseError(op, device+".used", ErrInvalidValue)
			}
			mount = fields[len(fields)-1]
		} else {
			// Filesystem 1B-blocks Used Available Use% Mounted on
			if len(fields) < 6 {
				return nil, newParseError(op, fields[0], ErrInvalidFormat)
			}
			device = fields[0]
			fsType = device // device column may be "tmpfs" etc.
			total, err = strconv.ParseUint(fields[1], 10, 64)
			if err != nil {
				return nil, newParseError(op, device+".total", ErrInvalidValue)
			}
			used, err = strconv.ParseUint(fields[2], 10, 64)
			if err != nil {
				return nil, newParseError(op, device+".used", ErrInvalidValue)
			}
			mount = fields[len(fields)-1]
		}

		if skipFSTypes[fsType] || skipFSTypes[device] {
			continue
		}

		results = append(results, DiskUsage{
			Device:     device,
			MountPoint: mount,
			UsedBytes:  used,
			TotalBytes: total,
		})
	}
	if err := scanner.Err(); err != nil {
		return nil, newParseError(op, "", err)
	}
	return results, nil
}
