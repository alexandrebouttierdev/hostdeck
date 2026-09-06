package parsers

import (
	"errors"
	"testing"
)

func TestParseOSRelease_Ubuntu2204Fixture(t *testing.T) {
	osr, err := ParseOSRelease(readFixture(t, "os_release"))
	if err != nil {
		t.Fatalf("ParseOSRelease: %v", err)
	}
	if osr.Name != "Ubuntu" {
		t.Errorf("Name = %q", osr.Name)
	}
	if osr.Version != "22.04.3 LTS (Jammy Jellyfish)" {
		t.Errorf("Version = %q", osr.Version)
	}
	if osr.PrettyName != "Ubuntu 22.04.3 LTS" {
		t.Errorf("PrettyName = %q", osr.PrettyName)
	}
	if osr.ID != "ubuntu" {
		t.Errorf("ID = %q", osr.ID)
	}
}

func TestParseOSRelease_Errors(t *testing.T) {
	if _, err := ParseOSRelease(""); !errors.Is(err, ErrEmptyInput) {
		t.Errorf("empty: %v", err)
	}
	partial := "NAME=\"Ubuntu\"\nID=ubuntu\n"
	if _, err := ParseOSRelease(partial); !errors.Is(err, ErrMissingField) {
		t.Errorf("missing: %v", err)
	}
}

func TestParseOSRelease_Unquoted(t *testing.T) {
	in := "NAME=Debian\nVERSION=12\nPRETTY_NAME=Debian GNU/Linux 12\nID=debian\n"
	osr, err := ParseOSRelease(in)
	if err != nil {
		t.Fatal(err)
	}
	if osr.Name != "Debian" || osr.ID != "debian" {
		t.Errorf("%+v", osr)
	}
}
