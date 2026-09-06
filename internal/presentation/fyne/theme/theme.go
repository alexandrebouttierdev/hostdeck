package theme

import (
	"image/color"

	"fyne.io/fyne/v2"
	fynetheme "fyne.io/fyne/v2/theme"
)

type HostDeckTheme struct{}

var _ fyne.Theme = (*HostDeckTheme)(nil)

func (t *HostDeckTheme) Color(name fyne.ThemeColorName, _ fyne.ThemeVariant) color.Color {
	switch name {
	case fynetheme.ColorNameBackground:
		return ColorBg
	case fynetheme.ColorNameButton:
		return ColorPanelRaised
	case fynetheme.ColorNameDisabledButton:
		return ColorPanel
	case fynetheme.ColorNameDisabled:
		return ColorTextMuted
	case fynetheme.ColorNameError:
		return ColorCritical
	case fynetheme.ColorNameFocus:
		return ColorAccent
	case fynetheme.ColorNameForeground:
		return ColorTextPrimary
	case fynetheme.ColorNameHover:
		return color.NRGBA{R: 0x1C, G: 0x23, B: 0x2C, A: 0xFF}
	case fynetheme.ColorNameInputBackground:
		return ColorPanel
	case fynetheme.ColorNameInputBorder:
		return ColorBorder
	case fynetheme.ColorNameMenuBackground:
		return ColorPanelRaised
	case fynetheme.ColorNameOverlayBackground:
		return color.NRGBA{A: 0x99}
	case fynetheme.ColorNamePlaceHolder:
		return ColorTextMuted
	case fynetheme.ColorNamePressed:
		return ColorSelection
	case fynetheme.ColorNamePrimary:
		return ColorAccent
	case fynetheme.ColorNameScrollBar:
		return ColorBorder
	case fynetheme.ColorNameSelection:
		return ColorSelection
	case fynetheme.ColorNameSeparator:
		return ColorBorder
	case fynetheme.ColorNameShadow:
		return color.NRGBA{A: 0x66}
	case fynetheme.ColorNameSuccess:
		return ColorOnline
	case fynetheme.ColorNameWarning:
		return ColorWarning
	case fynetheme.ColorNameHeaderBackground:
		return ColorRail
	default:
		return fynetheme.DefaultTheme().Color(name, fynetheme.VariantDark)
	}
}

func (t *HostDeckTheme) Font(style fyne.TextStyle) fyne.Resource {
	return fynetheme.DefaultTheme().Font(style)
}

func (t *HostDeckTheme) Icon(name fyne.ThemeIconName) fyne.Resource {
	return fynetheme.DefaultTheme().Icon(name)
}

func (t *HostDeckTheme) Size(name fyne.ThemeSizeName) float32 {
	switch name {
	case fynetheme.SizeNamePadding:
		return 6
	case fynetheme.SizeNameInnerPadding:
		return 8
	case fynetheme.SizeNameText:
		return 12
	case fynetheme.SizeNameCaptionText:
		return 11
	case fynetheme.SizeNameHeadingText:
		return 18
	case fynetheme.SizeNameSubHeadingText:
		return 14
	case fynetheme.SizeNameInputBorder:
		return 1
	case fynetheme.SizeNameScrollBar:
		return 8
	case fynetheme.SizeNameSeparatorThickness:
		return 1
	default:
		return fynetheme.DefaultTheme().Size(name)
	}
}
