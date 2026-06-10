import { alpha, createTheme } from '@mui/material/styles'
import type { Theme } from '@mui/material/styles'
import Grow from '@mui/material/Grow'

declare module '@mui/material/styles' {
  interface Theme {
    tokens: typeof tokens
  }
  interface ThemeOptions {
    tokens?: typeof tokens
  }
}

/**
 * MUI Theme — mirrors IntegrationManager.AdminPortal so the entire Urgent /
 * DFRNT product family shares one design system. Two variants are exposed:
 * - US tenants:    blue   (dfrntPrimaryPalette)
 * - non-US tenants: amber (urgentPrimaryPalette)
 *
 * If you change values here, mirror the same edit in
 * `intergrationmanager/src/IntegrationManager.AdminPortal/src/styles/theme.ts`.
 */

const dfrntPrimaryPalette = {
  50: '#e3f2fd',
  100: '#bbdefb',
  200: '#90caf9',
  300: '#64b5f6',
  400: '#42a5f5',
  500: '#2196f3',
  600: '#1e88e5',
  700: '#1976d2',
  800: '#1565c0',
  900: '#0d47a1',
  A100: '#82b1ff',
  A200: '#448aff',
  A400: '#2979ff',
  A700: '#2962ff',
}

const urgentPrimaryPalette = {
  50: '#fef9e7',
  100: '#fcefc4',
  200: '#fae49d',
  300: '#f8d976',
  400: '#f6d058',
  500: '#f4c430',
  600: '#e5b52a',
  700: '#d4a324',
  800: '#c3911e',
  900: '#a87614',
  A100: '#fff8e1',
  A200: '#ffecb3',
  A400: '#ffd54f',
  A700: '#ffc107',
}

const accentPalette = {
  50: '#fafaf9',
  100: '#f5f5f4',
  200: '#e7e5e4',
  300: '#d6d3d1',
  400: '#a8a29e',
  500: '#78716c',
  600: '#57534e',
  700: '#44403c',
  800: '#292524',
  900: '#1c1917',
}

const tokens = {
  radius: {
    xs: 2,
    sm: 6,
    md: 8,
    lg: 12,
    xl: 16,
    full: 9999,
    tile: 2,
  },
  duration: {
    instant: 100,
    fast: 150,
    normal: 200,
    slow: 350,
  },
  shadow: {
    sm: '0 1px 2px 0 rgba(0,0,0,.06), 0 1px 3px 0 rgba(0,0,0,.04)',
    md: '0 2px 4px -1px rgba(0,0,0,.06), 0 4px 6px -1px rgba(0,0,0,.04)',
    lg: '0 10px 15px -3px rgba(0,0,0,.1), 0 4px 6px -4px rgba(0,0,0,.1)',
    xl: '0 20px 25px -5px rgba(0,0,0,.1), 0 8px 10px -6px rgba(0,0,0,.1)',
  },
}

const sharedColors = {
  success: {
    main: '#4CAF50',
    light: '#81C784',
    dark: '#388E3C',
    lighter: '#E8F5E9',
    contrast: '#FFFFFF',
  },
  warning: {
    main: '#FF9800',
    light: '#FFB74D',
    dark: '#F57C00',
    lighter: '#FFF3E0',
    contrast: '#000000',
  },
  error: {
    main: '#F44336',
    light: '#E57373',
    dark: '#D32F2F',
    lighter: '#FFEBEE',
    contrast: '#FFFFFF',
  },
  info: {
    main: '#2196F3',
    light: '#64B5F6',
    dark: '#1976D2',
    lighter: '#E3F2FD',
    contrast: '#FFFFFF',
  },
  surface: {
    default: accentPalette[100],
    paper: '#FFFFFF',
    elevated: '#FFFFFF',
  },
  text: {
    primary: 'rgba(0, 0, 0, 0.87)',
    secondary: 'rgba(0, 0, 0, 0.64)',
    disabled: 'rgba(0, 0, 0, 0.38)',
    hint: 'rgba(0, 0, 0, 0.44)',
  },
  divider: 'rgba(0, 0, 0, 0.16)',
}

const APP_BG_SURFACE = accentPalette[50]

export function createAppTheme(isUsCustomer: boolean): Theme {
  const primaryPalette = isUsCustomer ? dfrntPrimaryPalette : urgentPrimaryPalette
  const primaryContrastText = isUsCustomer ? '#FFFFFF' : 'rgba(0, 0, 0, 0.87)'

  const colors = {
    primary: {
      main: primaryPalette[500],
      light: primaryPalette[300],
      dark: primaryPalette[700],
      darker: primaryPalette[900],
      lighter: primaryPalette[50],
      contrast: primaryContrastText,
    },
    secondary: {
      main: accentPalette[500],
      light: accentPalette[300],
      dark: accentPalette[600],
      darker: accentPalette[800],
      lighter: accentPalette[50],
      contrast: '#FFFFFF',
    },
    ...sharedColors,
  }

  return createTheme({
    tokens,
    palette: {
      mode: 'light',
      primary: {
        main: colors.primary.main,
        light: colors.primary.light,
        dark: colors.primary.dark,
        contrastText: colors.primary.contrast,
      },
      secondary: {
        main: colors.secondary.main,
        light: colors.secondary.light,
        dark: colors.secondary.dark,
        contrastText: colors.secondary.contrast,
      },
      success: {
        main: colors.success.main,
        light: colors.success.light,
        dark: colors.success.dark,
        contrastText: colors.success.contrast,
      },
      warning: {
        main: colors.warning.main,
        light: colors.warning.light,
        dark: colors.warning.dark,
        contrastText: colors.warning.contrast,
      },
      error: {
        main: colors.error.main,
        light: colors.error.light,
        dark: colors.error.dark,
        contrastText: colors.error.contrast,
      },
      info: {
        main: colors.info.main,
        light: colors.info.light,
        dark: colors.info.dark,
        contrastText: colors.info.contrast,
      },
      background: {
        default: colors.surface.default,
        paper: colors.surface.paper,
      },
      text: {
        primary: colors.text.primary,
        secondary: colors.text.secondary,
        disabled: colors.text.disabled,
      },
      divider: colors.divider,
      grey: accentPalette,
    },
    typography: {
      fontFamily:
        '"Plus Jakarta Sans Variable", "Plus Jakarta Sans", -apple-system, BlinkMacSystemFont, sans-serif',
      fontSize: 14,
      fontWeightLight: 300,
      fontWeightRegular: 400,
      fontWeightMedium: 500,
      fontWeightBold: 700,
      h1: {
        fontSize: '2.5rem',
        fontWeight: 700,
        lineHeight: 1.1,
        letterSpacing: '-0.03em',
      },
      h2: {
        fontSize: '1.75rem',
        fontWeight: 700,
        lineHeight: 1.2,
        letterSpacing: '-0.02em',
      },
      h3: {
        fontSize: '1.25rem',
        fontWeight: 700,
        lineHeight: 1.3,
        letterSpacing: '-0.01em',
      },
      h4: {
        fontSize: '1rem',
        fontWeight: 600,
        lineHeight: 1.35,
        letterSpacing: '0em',
      },
      h5: {
        fontSize: '0.9375rem',
        fontWeight: 600,
        lineHeight: 1.4,
        letterSpacing: '0em',
      },
      h6: {
        fontSize: '0.8125rem',
        fontWeight: 600,
        lineHeight: 1.4,
        letterSpacing: '0.01em',
        textTransform: 'uppercase',
        color: 'rgba(0, 0, 0, 0.64)',
      },
      subtitle1: {
        fontSize: '1rem',
        fontWeight: 400,
        lineHeight: 1.75,
        letterSpacing: '0.00938em',
      },
      subtitle2: {
        fontSize: '0.875rem',
        fontWeight: 500,
        lineHeight: 1.57,
        letterSpacing: '0.00714em',
      },
      body1: {
        fontSize: '0.875rem',
        fontWeight: 400,
        lineHeight: 1.5,
        letterSpacing: '0.00938em',
      },
      body2: {
        fontSize: '0.8125rem',
        fontWeight: 400,
        lineHeight: 1.43,
        letterSpacing: '0.01071em',
      },
      caption: {
        fontSize: '0.75rem',
        fontWeight: 400,
        lineHeight: 1.4,
        letterSpacing: '0.03333em',
      },
      overline: {
        fontSize: '0.625rem',
        fontWeight: 500,
        lineHeight: 2.5,
        letterSpacing: '0.08333em',
        textTransform: 'uppercase',
      },
      button: {
        fontSize: '0.875rem',
        fontWeight: 500,
        textTransform: 'none',
        letterSpacing: '0.01em',
      },
    },
    shape: {
      borderRadius: tokens.radius.md,
    },
    spacing: 8,
    shadows: [
      'none',
      tokens.shadow.sm,
      tokens.shadow.sm,
      tokens.shadow.md,
      tokens.shadow.md,
      tokens.shadow.md,
      tokens.shadow.lg,
      tokens.shadow.lg,
      tokens.shadow.lg,
      tokens.shadow.lg,
      tokens.shadow.lg,
      tokens.shadow.lg,
      tokens.shadow.xl,
      tokens.shadow.xl,
      tokens.shadow.xl,
      tokens.shadow.xl,
      tokens.shadow.xl,
      tokens.shadow.xl,
      tokens.shadow.xl,
      tokens.shadow.xl,
      tokens.shadow.xl,
      tokens.shadow.xl,
      tokens.shadow.xl,
      tokens.shadow.xl,
      tokens.shadow.xl,
    ],
    components: {
      MuiCssBaseline: {
        styleOverrides: {
          body: {
            scrollbarWidth: 'thin',
            scrollbarColor: `${accentPalette[400]} transparent`,
            minHeight: '100vh',
            backgroundColor: APP_BG_SURFACE,
            WebkitFontSmoothing: 'antialiased',
            MozOsxFontSmoothing: 'grayscale',
          },
        },
      },
      MuiButton: {
        defaultProps: {
          disableElevation: true,
        },
        styleOverrides: {
          root: {
            borderRadius: tokens.radius.full,
            padding: '8px 20px',
            fontWeight: 600,
            fontSize: '0.875rem',
            textTransform: 'none' as const,
            letterSpacing: '0.01em',
            minHeight: 40,
            transition: `all ${tokens.duration.normal}ms cubic-bezier(0.4, 0, 0.2, 1)`,
          },
          sizeSmall: {
            padding: '4px 14px',
            fontSize: '0.8125rem',
            minHeight: 32,
          },
          sizeLarge: {
            padding: '12px 28px',
            fontSize: '0.9375rem',
            minHeight: 48,
          },
          contained: {
            boxShadow: 'none',
            '&:hover': {
              boxShadow: tokens.shadow.sm,
            },
          },
          outlined: {
            borderColor: colors.divider,
            '&:hover': {
              borderColor: colors.primary.main,
              backgroundColor: alpha(colors.primary.main, 0.04),
            },
          },
          text: {
            '&:hover': {
              backgroundColor: alpha(colors.primary.main, 0.06),
            },
          },
        },
      },
      MuiIconButton: {
        styleOverrides: {
          root: {
            borderRadius: '50%',
            transition: `background-color ${tokens.duration.fast}ms`,
            '&:hover': {
              backgroundColor: alpha(colors.primary.main, 0.04),
            },
          },
        },
      },
      MuiPaper: {
        defaultProps: {
          elevation: 0,
        },
        styleOverrides: {
          root: {
            backgroundImage: 'none',
          },
          rounded: {
            borderRadius: tokens.radius.md,
          },
          elevation1: {
            boxShadow: tokens.shadow.sm,
          },
          elevation2: {
            boxShadow: tokens.shadow.sm,
          },
          elevation3: {
            boxShadow: tokens.shadow.md,
          },
          elevation4: {
            boxShadow: tokens.shadow.md,
          },
        },
      },
      MuiCard: {
        defaultProps: {
          variant: 'outlined',
        },
        styleOverrides: {
          root: {
            borderRadius: tokens.radius.lg,
            borderColor: 'rgba(0, 0, 0, 0.08)',
            boxShadow: 'none',
            backgroundColor: '#FFFFFF',
            transition: `box-shadow ${tokens.duration.normal}ms cubic-bezier(0.4, 0, 0.2, 1)`,
          },
        },
      },
      MuiCardContent: {
        styleOverrides: {
          root: {
            padding: 20,
            '&:last-child': {
              paddingBottom: 20,
            },
          },
        },
      },
      MuiDialog: {
        defaultProps: {
          slots: { transition: Grow },
          slotProps: {
            paper: {
              elevation: 8,
            },
          },
        },
        styleOverrides: {
          paper: {
            borderRadius: tokens.radius.xl,
            boxShadow:
              '0 24px 48px -12px rgba(0,0,0,.18), 0 0 0 1px rgba(0,0,0,.04)',
          },
        },
      },
      MuiDialogTitle: {
        styleOverrides: {
          root: {
            fontSize: '1.125rem',
            fontWeight: 600,
            padding: '20px 24px',
            color: colors.text.primary,
            borderBottom: `1px solid ${colors.divider}`,
          },
        },
      },
      MuiDialogContent: {
        styleOverrides: {
          root: {
            padding: '24px 24px',
          },
        },
      },
      MuiDialogActions: {
        styleOverrides: {
          root: {
            padding: '16px 24px 24px',
            gap: 8,
            borderTop: `1px solid ${colors.divider}`,
          },
        },
      },
      MuiTextField: {
        styleOverrides: {
          root: {
            '& .MuiOutlinedInput-root': {
              borderRadius: tokens.radius.md,
              '& fieldset': {
                borderColor: colors.divider,
              },
              '&:hover fieldset': {
                borderColor: colors.text.secondary,
              },
              '&.Mui-focused fieldset': {
                borderWidth: 2,
                borderColor: colors.primary.main,
              },
            },
          },
        },
      },
      MuiOutlinedInput: {
        styleOverrides: {
          root: {
            borderRadius: tokens.radius.md,
            '& fieldset': {
              borderColor: colors.divider,
            },
            '&:hover fieldset': {
              borderColor: colors.text.secondary,
            },
          },
          input: {
            padding: '12px 14px',
          },
        },
      },
      MuiInputLabel: {
        styleOverrides: {
          root: {
            fontSize: '0.875rem',
            '&.Mui-focused': {
              color: colors.primary.main,
            },
          },
        },
      },
      MuiChip: {
        styleOverrides: {
          root: {
            borderRadius: 16,
            fontWeight: 400,
            fontSize: '0.8125rem',
          },
        },
      },
      MuiTab: {
        styleOverrides: {
          root: {
            textTransform: 'none' as const,
            fontWeight: 500,
            fontSize: '0.875rem',
            letterSpacing: '0.01em',
            minHeight: 48,
            padding: '12px 16px',
          },
        },
      },
      MuiTabs: {
        styleOverrides: {
          root: {
            minHeight: 48,
          },
          indicator: {
            height: 2,
          },
        },
      },
      MuiTableContainer: {
        styleOverrides: {
          root: {
            borderRadius: tokens.radius.md,
            border: `1px solid ${colors.divider}`,
            overflow: 'hidden',
          },
        },
      },
      MuiTableCell: {
        styleOverrides: {
          root: {
            fontSize: '0.8125rem',
            padding: '12px 16px',
            borderColor: colors.divider,
          },
          head: {
            fontWeight: 600,
            color: colors.text.secondary,
            fontSize: '0.8125rem',
            textTransform: 'none' as const,
            letterSpacing: 'normal',
            backgroundColor: accentPalette[50],
            borderBottomWidth: 1,
            borderBottomColor: colors.divider,
          },
        },
      },
      MuiTableRow: {
        styleOverrides: {
          root: {
            transition: `background-color ${tokens.duration.fast}ms`,
            '&:nth-of-type(even)': {
              backgroundColor: alpha(accentPalette[100], 0.5),
            },
            '&:hover': {
              backgroundColor: alpha(colors.primary.main, 0.06),
            },
          },
        },
      },
      MuiTooltip: {
        styleOverrides: {
          tooltip: {
            backgroundColor: accentPalette[700],
            fontSize: '0.75rem',
            fontWeight: 400,
            padding: '6px 10px',
            borderRadius: tokens.radius.sm,
          },
        },
      },
      MuiDivider: {
        styleOverrides: {
          root: {
            borderColor: colors.divider,
          },
        },
      },
      MuiMenu: {
        styleOverrides: {
          paper: {
            borderRadius: tokens.radius.md,
            boxShadow: tokens.shadow.lg,
          },
        },
      },
      MuiMenuItem: {
        styleOverrides: {
          root: {
            fontSize: '0.875rem',
            padding: '8px 16px',
            minHeight: 40,
            '&:hover': {
              backgroundColor: alpha(colors.primary.main, 0.04),
            },
          },
        },
      },
      MuiDrawer: {
        styleOverrides: {
          paper: {
            backgroundColor: accentPalette[50],
          },
        },
      },
      MuiListItemButton: {
        styleOverrides: {
          root: {
            '&.Mui-selected': {
              backgroundColor: alpha(colors.primary.main, 0.08),
              '&:hover': {
                backgroundColor: alpha(colors.primary.main, 0.12),
              },
            },
          },
        },
      },
      MuiAutocomplete: {
        styleOverrides: {
          listbox: {
            maxHeight: 300,
            overflow: 'auto',
          },
        },
      },
      MuiAlert: {
        styleOverrides: {
          root: {
            borderRadius: tokens.radius.md,
          },
        },
      },
      MuiLinearProgress: {
        styleOverrides: {
          root: {
            borderRadius: tokens.radius.full,
            height: 6,
            backgroundColor: alpha(colors.primary.main, 0.12),
          },
          bar: {
            borderRadius: tokens.radius.full,
          },
        },
      },
      MuiCircularProgress: {
        styleOverrides: {
          root: {
            strokeLinecap: 'round',
          },
        },
      },
    },
  })
}

export const lightTheme = createAppTheme(true)

// Default export kept for callers that import `theme` directly. Passenger-facing
// routes have no auth context at mount time, so we render the US/blue variant
// by default; a per-tenant override can be wired in later if we surface tenant
// info on the booking summary.
export const theme = lightTheme

export const brandColors = {
  mainDark: dfrntPrimaryPalette[900],
  mainHighlight: dfrntPrimaryPalette[500],
  secondary: accentPalette[500],
  surfaceWhite: '#FFFFFF',
  surfaceLight: accentPalette[100],
  surfaceDark: accentPalette[900],
  textPrimary: 'rgba(0, 0, 0, 0.87)',
  textSecondary: 'rgba(0, 0, 0, 0.64)',
  textMuted: 'rgba(0, 0, 0, 0.44)',
  success: sharedColors.success.main,
  warning: sharedColors.warning.main,
  error: sharedColors.error.main,
  info: sharedColors.info.main,
}

export { tokens }
