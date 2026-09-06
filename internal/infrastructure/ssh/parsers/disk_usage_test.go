package parsers

import (
	"errors"
	"testing"
)

func TestParseDiskUsage_Ubuntu2204Fixture(t *testing.T) {
	disks, err := ParseDiskUsage(readFixture(t, "df_B1"))
	if err != nil {
		t.Fatalf("ParseDiskUsage: %v", err)
	}
	if len(disks) != 3 {
		t.Fatalf("got %d disks, want 3: %+v", len(disks), disks)
	}
	byMount := map[string]DiskUsage{}
	for _, d := range disks {
		byMount[d.MountPoint] = d
	}
	root := byMount["/"]
	if root.Device != "/dev/sda1" || root.TotalBytes != 107374182400 || root.UsedBytes != 53687091200 {
		t.Errorf("root = %+v", root)
	}
	data := byMount["/data"]
	if data.Device != "/dev/sdb1" || data.TotalBytes != 21474836480 {
		t.Errorf("data = %+v", data)
	}
	home := byMount["/home"]
	if home.Device != "/dev/sda2" {
		t.Errorf("home = %+v", home)
	}
	for _, d := range disks {
		if d.Device == "tmpfs" || d.Device == "devtmpfs" || d.Device == "squashfs" {
			t.Errorf("should skip %s", d.Device)
		}
	}
}

func TestParseDiskUsage_WithTypeColumn(t *testing.T) {
	in := `Filesystem     Type     1B-blocks        Used  Available Use% Mounted on
/dev/sda1      ext4    1000000000   500000000  500000000  50% /
tmpfs          tmpfs     100000000           0  100000000   0% /run
`
	disks, err := ParseDiskUsage(in)
	if err != nil {
		t.Fatal(err)
	}
	if len(disks) != 1 || disks[0].MountPoint != "/" {
		t.Fatalf("got %+v", disks)
	}
}

func TestParseDiskUsage_Errors(t *testing.T) {
	if _, err := ParseDiskUsage(""); !errors.Is(err, ErrEmptyInput) {
		t.Errorf("empty: %v", err)
	}
	bad := "Filesystem 1B-blocks Used Available Use% Mounted on\n/dev/sda1 abc 1 1 1% /\n"
	if _, err := ParseDiskUsage(bad); !errors.Is(err, ErrInvalidValue) {
		t.Errorf("invalid: %v", err)
	}
}
