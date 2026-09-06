package theme

import "image/color"

var (
	ColorBg            = color.NRGBA{R: 0x0B, G: 0x0E, B: 0x14, A: 0xFF}
	ColorRail          = color.NRGBA{R: 0x0D, G: 0x11, B: 0x17, A: 0xFF}
	ColorPanel         = color.NRGBA{R: 0x12, G: 0x16, B: 0x1C, A: 0xFF}
	ColorPanelRaised   = color.NRGBA{R: 0x16, G: 0x1B, B: 0x22, A: 0xFF}
	ColorBorder        = color.NRGBA{R: 0x2A, G: 0x33, B: 0x40, A: 0xFF}
	ColorAccent        = color.NRGBA{R: 0x00, G: 0xA3, B: 0xFF, A: 0xFF}
	ColorSelection     = color.NRGBA{R: 0x00, G: 0xA3, B: 0xFF, A: 0x33}
	ColorTextPrimary   = color.NRGBA{R: 0xE6, G: 0xED, B: 0xF3, A: 0xFF}
	ColorTextSecondary = color.NRGBA{R: 0x8B, G: 0x94, B: 0xA3, A: 0xFF}
	ColorTextMuted     = color.NRGBA{R: 0x6E, G: 0x76, B: 0x81, A: 0xFF}
	ColorOnline        = color.NRGBA{R: 0x3F, G: 0xB9, B: 0x50, A: 0xFF}
	ColorWarning       = color.NRGBA{R: 0xD2, G: 0x99, B: 0x22, A: 0xFF}
	ColorCritical      = color.NRGBA{R: 0xF8, G: 0x51, B: 0x49, A: 0xFF}
	ColorHigh          = color.NRGBA{R: 0xF0, G: 0x88, B: 0x3E, A: 0xFF}
	ColorInfo          = color.NRGBA{R: 0x58, G: 0xA6, B: 0xFF, A: 0xFF}
	ColorOffline       = color.NRGBA{R: 0x6E, G: 0x76, B: 0x81, A: 0xFF}
	ColorThreshold     = color.NRGBA{R: 0xF8, G: 0x51, B: 0x49, A: 0xAA}
	ColorSeriesBlue    = color.NRGBA{R: 0x00, G: 0xA3, B: 0xFF, A: 0xFF}
	ColorSeriesPurple  = color.NRGBA{R: 0xA3, G: 0x71, B: 0xF7, A: 0xFF}
	ColorSeriesOrange  = color.NRGBA{R: 0xF0, G: 0x88, B: 0x3E, A: 0xFF}
	ColorSeriesGreen   = color.NRGBA{R: 0x3F, G: 0xB9, B: 0x50, A: 0xFF}
	ColorSeriesCyan    = color.NRGBA{R: 0x00, G: 0xD4, B: 0xAA, A: 0xFF}
	ColorSeriesRed     = color.NRGBA{R: 0xFF, G: 0x7B, B: 0x72, A: 0xFF}
)

func SeverityColor(sev string) color.Color {
	switch sev {
	case "critical":
		return ColorCritical
	case "high":
		return ColorHigh
	case "warning":
		return ColorWarning
	case "average", "information":
		return ColorInfo
	default:
		return ColorTextMuted
	}
}

func StatusColor(status string) color.Color {
	switch status {
	case "online":
		return ColorOnline
	case "warning":
		return ColorWarning
	case "offline", "unreachable", "auth_failed":
		return ColorCritical
	case "maintenance":
		return ColorInfo
	default:
		return ColorOffline
	}
}
