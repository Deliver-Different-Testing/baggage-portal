import { createTheme } from '@mui/material/styles'

// Palette mirrors the DFRNT design system used in the template HTML files,
// remapped onto MUI tokens. Brand dark = #001F3D, accent cyan = #00B0B9.
// Font is Plus Jakarta Sans (loaded via @fontsource-variable in main.tsx)
// rather than Inter — the rest of the Urgent stack standardised on PJS.
export const theme = createTheme({
  palette: {
    mode: 'light',
    primary: { main: '#001F3D', light: '#33486a', dark: '#000d22' },
    secondary: { main: '#00B0B9', dark: '#008a92', light: '#33c3ca' },
    success: { main: '#13b964' },
    warning: { main: '#fe811a' },
    error: { main: '#dc3246' },
    background: { default: '#f4f2f1', paper: '#ffffff' },
    text: { primary: '#0d0c2c', secondary: '#6e6d80', disabled: '#9e9da8' },
  },
  typography: {
    fontFamily: '"Plus Jakarta Sans Variable", "Plus Jakarta Sans", -apple-system, BlinkMacSystemFont, "Segoe UI", sans-serif',
    h1: { fontWeight: 700, fontSize: 28 },
    h2: { fontWeight: 700, fontSize: 22 },
    h3: { fontWeight: 700, fontSize: 18 },
    button: { fontWeight: 700, textTransform: 'none' },
  },
  shape: { borderRadius: 12 },
  components: {
    MuiButton: {
      styleOverrides: {
        root: { borderRadius: 999, paddingInline: 24 },
      },
    },
    MuiCard: {
      styleOverrides: {
        root: { boxShadow: '0 1px 3px rgba(13,12,44,0.06)' },
      },
    },
  },
})
