package parsers

import (
	"errors"
	"testing"
)

func TestParseNetworkDev_Ubuntu2204Fixture(t *testing.T) {
	ifaces, err := ParseNetworkDev(readFixture(t, "proc_net_dev"))
	if err != nil {
		t.Fatalf("ParseNetworkDev: %v", err)
	}
	if _, ok := ifaces["lo"]; ok {
		t.Error("lo should be skipped")
	}
	if len(ifaces) != 3 {
		t.Fatalf("got %d interfaces, want 3: %v", len(ifaces), ifaces)
	}
	eth0 := ifaces["eth0"]
	if eth0.RXBytes != 9876543210 || eth0.TXBytes != 5432109876 {
		t.Errorf("eth0 = %+v", eth0)
	}
	eth1 := ifaces["eth1"]
	if eth1.RXBytes != 112233445 || eth1.TXBytes != 556677889 {
		t.Errorf("eth1 = %+v", eth1)
	}
	docker0 := ifaces["docker0"]
	if docker0.RXBytes != 99887766 || docker0.TXBytes != 88776655 {
		t.Errorf("docker0 = %+v", docker0)
	}
}

func TestParseNetworkDev_Errors(t *testing.T) {
	if _, err := ParseNetworkDev(""); !errors.Is(err, ErrEmptyInput) {
		t.Errorf("empty: %v", err)
	}
	short := "Inter-| Receive | Transmit\nface |bytes|bytes\neth0: 1 2\n"
	if _, err := ParseNetworkDev(short); !errors.Is(err, ErrInvalidFormat) {
		t.Errorf("short fields: %v", err)
	}
	bad := "Inter-| Receive | Transmit\nface |bytes|bytes\neth0: abc 0 0 0 0 0 0 0 10 0 0 0 0 0 0 0\n"
	if _, err := ParseNetworkDev(bad); !errors.Is(err, ErrInvalidValue) {
		t.Errorf("invalid rx: %v", err)
	}
}

func TestParseNetworkDev_MultipleInterfacesOnlyLo(t *testing.T) {
	in := `Inter-|   Receive                                                |  Transmit
 face |bytes    packets errs drop fifo frame compressed multicast|bytes    packets errs drop fifo colls carrier compressed
    lo: 100 1 0 0 0 0 0 0 100 1 0 0 0 0 0 0
`
	ifaces, err := ParseNetworkDev(in)
	if err != nil {
		t.Fatal(err)
	}
	if len(ifaces) != 0 {
		t.Errorf("expected empty map, got %v", ifaces)
	}
}
