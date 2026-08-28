import {
  createTheme,
  v8CssVariablesResolver,
  type CSSVariablesResolver,
  type MantineColorsTuple,
} from '@mantine/core';
import { getMd3Scheme } from './md3';

const brand: MantineColorsTuple = [
  '#e7f8fe', '#d8f4fd', '#b1e9fb', '#82dcf8', '#5bd1f5',
  '#3bc7f4', '#1eb2e6', '#1590c0', '#0f6f96', '#0a4d69',
];

const ink: MantineColorsTuple = [
  '#ecebf1', '#cfced5', '#a8a7b6', '#83829a', '#6e6d80',
  '#4f4e66', '#35334f', '#211f40', '#141233', '#0d0c2c',
];

const reflex: MantineColorsTuple = [
  '#eaeeff', '#d4dcff', '#aab8ff', '#7d92ff', '#5670ff',
  '#2a4eff', '#233fd6', '#1b31a8', '#132279', '#0b134a',
];

const grape: MantineColorsTuple = [
  '#f3edfc', '#e6dbf9', '#cdb7f3', '#b088ec', '#9865e5',
  '#824ae0', '#6c3bbe', '#552e95', '#3f226e', '#291546',
];

const green: MantineColorsTuple = [
  '#e5f8ee', '#d0f1e0', '#a1e3c1', '#5fd199', '#2ec27a',
  '#13b964', '#0f9d55', '#0b7d44', '#085e33', '#053f22',
];

const orange: MantineColorsTuple = [
  '#fff3e6', '#ffe6d1', '#ffcda3', '#ffab63', '#fe9333',
  '#fe811a', '#e06d10', '#b3560b', '#853f08', '#582905',
];

const red: MantineColorsTuple = [
  '#fdeaec', '#f8d6da', '#f1adb5', '#e97b88', '#e25062',
  '#dc3246', '#bb2a3c', '#93212f', '#6c1823', '#460f16',
];

const gray: MantineColorsTuple = [
  '#ffffff', '#f8f7f7', '#f4f2f1', '#e7e5e4', '#d6d3d1',
  '#a8a29e', '#78716c', '#57534e', '#44403c', '#292524',
];

const D = getMd3Scheme(true, 'dark');
const dark: MantineColorsTuple = [
  D.onSurface,
  D.onSurfaceVariant,
  D.outline,
  '#6f6c75',
  D.outlineVariant,
  D.surfaceContainerHighest,
  D.surfaceContainerHigh,
  D.surface,
  D.surfaceDim,
  D.surfaceContainerLowest,
];

export const sidebarColors = {
  appBar: ink[9],
  bg: ink[8],
  border: 'rgba(255, 255, 255, 0.10)',
  textPrimary: 'rgba(255, 255, 255, 0.95)',
  textSecondary: 'rgba(255, 255, 255, 0.60)',
  textMuted: 'rgba(255, 255, 255, 0.38)',
  hoverBg: 'rgba(255, 255, 255, 0.08)',
};

export const onBrandScrim = {
  heroBg: 'var(--mantine-color-ink-9)',
  text: '#fff',
  bodyText: 'rgba(255,255,255,0.85)',
  subtleText: 'rgba(255,255,255,0.8)',
  mutedText: 'rgba(255,255,255,0.7)',
  fill: 'rgba(255,255,255,0.15)',
  fillStrong: 'rgba(255,255,255,0.2)',
  hoverFill: 'rgba(255,255,255,0.1)',
  faintHoverFill: 'rgba(255,255,255,0.08)',
  solidHoverFill: 'rgba(255,255,255,0.9)',
  border: 'rgba(255,255,255,0.4)',
};

export const codeBlockPalette = {
  background: '#141233',
  backgroundError: '#2e1a1a',
  textSuccess: '#5fd199',
  textInfo: '#7d92ff',
  textError: '#f1adb5',
};

const HERO_SIZE = 'clamp(32px, 8vw, 40px)';

export const tokens = {
  radius: { xs: 4, sm: 8, md: 12, lg: 16, xl: 28, full: 9999, tile: 2 },
  type: {
    hero: HERO_SIZE,
    heroTitle: {
      fontSize: HERO_SIZE,
      lineHeight: 1.05,
      fontWeight: 700,
      letterSpacing: '-0.03em',
    } as const,
    sectionTitle: {
      fontSize: 18,
      fontWeight: 600,
      lineHeight: 1.3,
      letterSpacing: '-0.01em',
    } as const,
    figure: 'clamp(24px, 6vw, 28px)',
    eyebrow: {
      fontSize: 10,
      fontWeight: 700,
      letterSpacing: '0.12em',
      textTransform: 'uppercase',
    } as const,
  },
  hero: {
    px: { base: 20, sm: 32 },
    pt: { base: 32, sm: 40 },
    pb: { base: 48, sm: 56 },
    overlap: { base: -28, sm: -32 },
    measure: 520,
  },
  button: {
    primary: {
      minHeight: 56,
      fontSize: 16,
      fontWeight: 700,
      letterSpacing: '0.01em',
    } as const,
  },
  duration: { instant: 100, fast: 150, normal: 200, slow: 350 },
  shadow: {
    sm: '0 1px 3px 0 rgba(0,0,0,.1), 0 1px 2px -1px rgba(0,0,0,.1)',
    md: '0 4px 6px -1px rgba(0,0,0,.1), 0 2px 4px -2px rgba(0,0,0,.1)',
    lg: '0 10px 15px -3px rgba(0,0,0,.1), 0 4px 6px -4px rgba(0,0,0,.1)',
    xl: '0 20px 25px -5px rgba(0,0,0,.1), 0 8px 10px -6px rgba(0,0,0,.1)',
  },
};

export const brandColors = {
  mainDark: ink[9],
  mainHighlight: brand[5],
  secondary: gray[6],
  surfaceWhite: '#ffffff',
  surfaceLight: gray[2],
  surfaceDark: ink[9],
  textPrimary: ink[9],
  textSecondary: 'rgba(13,12,44,0.64)',
  textMuted: 'rgba(13,12,44,0.44)',
  success: green[5],
  warning: orange[5],
  error: red[5],
  info: reflex[5],
};

declare module '@mantine/core' {
  export interface MantineThemeOther {
    shell: typeof sidebarColors;
    scrim: typeof onBrandScrim;
    code: typeof codeBlockPalette;
    tokens: typeof tokens;
  }
}

export const dfrntTheme = createTheme({
  primaryColor: 'brand',
  primaryShade: { light: 5, dark: 4 },
  autoContrast: true,
  luminanceThreshold: 0.4,
  colors: { brand, ink, reflex, grape, green, orange, red, gray, dark },
  white: '#ffffff',
  black: ink[9],
  fontFamily: '"Plus Jakarta Sans Variable", "Plus Jakarta Sans", -apple-system, BlinkMacSystemFont, sans-serif',
  fontFamilyMonospace: 'ui-monospace, SFMono-Regular, Menlo, monospace',
  headings: {
    fontFamily: '"Plus Jakarta Sans Variable", "Plus Jakarta Sans", sans-serif',
    fontWeight: '600',
  },
  defaultRadius: 'md',
  radius: { xs: '4px', sm: '8px', md: '12px', lg: '16px', xl: '28px' },
  components: {
    Button: { defaultProps: { radius: 9999 } },
    ActionIcon: { defaultProps: { radius: 9999 } },
    Card: {
      defaultProps: { radius: 'md' },
      styles: {
        root: {
          backgroundColor: 'var(--dd-surface-container)',
          border: '1px solid var(--dd-outline-variant)',
        },
      },
    },
    Paper: {
      defaultProps: { radius: 'md' },
      styles: {
        root: {
          backgroundColor: 'var(--dd-surface-container)',
          border: '1px solid var(--dd-outline-variant)',
        },
      },
    },
    Modal: {
      defaultProps: { radius: 'xl', centered: true },
      styles: { content: { backgroundColor: 'var(--dd-surface-container-high)' } },
    },
    Checkbox: { defaultProps: { size: 'md', iconColor: 'var(--dd-on-brand-fill)' } },
    Radio: { defaultProps: { size: 'md', iconColor: 'var(--dd-on-brand-fill)' } },
    TextInput: { defaultProps: { size: 'md', radius: 'sm' } },
    Textarea: { defaultProps: { size: 'md', radius: 'sm' } },
    Select: { defaultProps: { size: 'md', radius: 'sm' } },
    Autocomplete: { defaultProps: { size: 'md', radius: 'sm' } },
    Menu: {
      defaultProps: { radius: 'sm', shadow: 'md' },
      styles: { dropdown: { backgroundColor: 'var(--dd-surface-container-high)' } },
    },
    Tooltip: { defaultProps: { radius: 'sm', color: 'ink' } },
    Badge: { defaultProps: { radius: 'sm' } },
    Tabs: { styles: { tab: { paddingBlock: 14, fontWeight: 500 } } },
  },
  other: {
    shell: sidebarColors,
    scrim: onBrandScrim,
    code: codeBlockPalette,
    tokens,
  },
});

export const dfrntCssVariablesResolver: CSSVariablesResolver = (theme) => {
  const light = getMd3Scheme(true, 'light');
  const dk = getMd3Scheme(true, 'dark');
  const v8 = v8CssVariablesResolver(theme);
  return {
    variables: { ...v8.variables },
    light: {
      ...v8.light,
      '--mantine-color-body': light.surface,
      '--dd-surface': light.surface,
      '--dd-surface-container': light.surfaceContainerLowest,
      '--dd-surface-container-high': light.surfaceContainerLowest,
      '--dd-outline-variant': light.outlineVariant,
      '--dd-on-brand-fill': ink[9],
      '--dd-on-brand-tint': brand[8],
      '--mantine-color-disabled-color': gray[7],
    },
    dark: {
      ...v8.dark,
      '--mantine-color-body': dk.surface,
      '--dd-surface': dk.surface,
      '--dd-surface-container': dk.surfaceContainer,
      '--dd-surface-container-high': dk.surfaceContainerHigh,
      '--dd-outline-variant': dk.outlineVariant,
      '--dd-on-brand-fill': ink[9],
      '--dd-on-brand-tint': brand[2],
      '--mantine-color-error': red[3],
      '--mantine-color-disabled': dk.surfaceDim,
      '--mantine-color-disabled-color': dk.onSurfaceVariant,
    },
  };
};
