import { createTheme } from '@mui/material/styles'

/** Monospace stack for numbers, coordinates and timings. */
export const monoFontFamily = "'JetBrains Mono', ui-monospace, SFMono-Regular, Menlo, monospace"

/** Colors from the "Salient UI" design mockup. */
export const tokens = {
  background: '#0F1115',
  surface: '#171A21',
  surfaceRaised: '#1E222B',
  appBar: '#14171D',
  border: '#2A2F3A',
  borderStrong: '#3A4252',
  textPrimary: '#E6E8EE',
  textSecondary: '#A8AEBB',
  primary: '#7AB0FF',
  onPrimary: '#0B1424',
  keypoint: '#FFB547',
  success: '#5FD08F',
  rejected: '#FF7A93',
} as const

export const theme = createTheme({
  cssVariables: true,
  palette: {
    mode: 'dark',
    background: { default: tokens.background, paper: tokens.surface },
    primary: { main: tokens.primary, contrastText: tokens.onPrimary },
    secondary: { main: tokens.keypoint },
    success: { main: tokens.success },
    error: { main: tokens.rejected },
    text: { primary: tokens.textPrimary, secondary: tokens.textSecondary },
    divider: tokens.border,
  },
  shape: { borderRadius: 8 },
  typography: {
    fontFamily: "Inter, system-ui, -apple-system, 'Segoe UI', sans-serif",
    fontSize: 14,
    button: { textTransform: 'none', fontWeight: 600 },
    overline: { fontWeight: 600, letterSpacing: '0.08em' },
  },
  components: {
    MuiPaper: {
      styleOverrides: {
        root: { backgroundImage: 'none' },
        outlined: { borderColor: tokens.border },
      },
    },
    MuiAppBar: {
      defaultProps: { elevation: 0, color: 'transparent' },
      styleOverrides: {
        root: { backgroundColor: tokens.appBar, borderBottom: `1px solid ${tokens.border}` },
      },
    },
    MuiButton: {
      defaultProps: { disableElevation: true },
    },
    MuiTab: {
      styleOverrides: { root: { textTransform: 'none', fontWeight: 500, minHeight: 64 } },
    },
  },
})
