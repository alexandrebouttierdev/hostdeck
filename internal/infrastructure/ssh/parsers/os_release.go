package parsers

import (
	"bufio"
	"strings"
)

// OSRelease holds selected fields from /etc/os-release.
type OSRelease struct {
	Name       string
	Version    string
	PrettyName string
	ID         string
}

// ParseOSRelease parses /etc/os-release for NAME, VERSION, PRETTY_NAME, and ID.
func ParseOSRelease(input string) (OSRelease, error) {
	const op = "ParseOSRelease"
	if strings.TrimSpace(input) == "" {
		return OSRelease{}, newParseError(op, "", ErrEmptyInput)
	}

	wanted := map[string]*string{}
	var info OSRelease
	wanted["NAME"] = &info.Name
	wanted["VERSION"] = &info.Version
	wanted["PRETTY_NAME"] = &info.PrettyName
	wanted["ID"] = &info.ID
	found := make(map[string]bool, len(wanted))

	scanner := bufio.NewScanner(strings.NewReader(input))
	for scanner.Scan() {
		line := strings.TrimSpace(scanner.Text())
		if line == "" || strings.HasPrefix(line, "#") {
			continue
		}
		eq := strings.IndexByte(line, '=')
		if eq <= 0 {
			continue
		}
		key := line[:eq]
		ptr, ok := wanted[key]
		if !ok {
			continue
		}
		val := line[eq+1:]
		val = unquote(val)
		*ptr = val
		found[key] = true
	}
	if err := scanner.Err(); err != nil {
		return OSRelease{}, newParseError(op, "", err)
	}

	for key := range wanted {
		if !found[key] {
			return OSRelease{}, newParseError(op, key, ErrMissingField)
		}
	}
	return info, nil
}

func unquote(s string) string {
	s = strings.TrimSpace(s)
	if len(s) >= 2 {
		if (s[0] == '"' && s[len(s)-1] == '"') || (s[0] == '\'' && s[len(s)-1] == '\'') {
			return s[1 : len(s)-1]
		}
	}
	return s
}
