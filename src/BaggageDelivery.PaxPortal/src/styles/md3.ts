export type Md3Mode = 'light' | 'dark';
export type Brand = 'dfrnt' | 'urgent';

export const SEEDS: Record<Brand, string> = {
  dfrnt: '#3bc7f4',
  urgent: '#3bc7f4',
};

export const dfrntPrimaryPalette = {
  50: '#e7f8fe',
  100: '#d8f4fd',
  200: '#b1e9fb',
  300: '#82dcf8',
  400: '#5bd1f5',
  500: '#3bc7f4',
  600: '#1eb2e6',
  700: '#1590c0',
  800: '#0f6f96',
  900: '#0a4d69',
  A100: '#b1e9fb',
  A200: '#82dcf8',
  A400: '#3bc7f4',
  A700: '#1590c0',
};

export const urgentPrimaryPalette = {
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
};

export const accentPalette = {
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
};

const RAMPS: Record<Brand, typeof dfrntPrimaryPalette> = {
  dfrnt: dfrntPrimaryPalette,
  urgent: urgentPrimaryPalette,
};

export interface Md3Scheme {
  primary: string;
  primaryLight: string;
  primaryDark: string;
  onPrimary: string;
  primaryContainer: string;
  onPrimaryContainer: string;
  secondary: string;
  onSecondary: string;
  secondaryContainer: string;
  onSecondaryContainer: string;
  surface: string;
  onSurface: string;
  onSurfaceVariant: string;
  surfaceContainerLowest: string;
  surfaceContainerLow: string;
  surfaceContainer: string;
  surfaceContainerHigh: string;
  surfaceContainerHighest: string;
  surfaceDim: string;
  surfaceBright: string;
  outline: string;
  outlineVariant: string;
  inverseSurface: string;
  inverseOnSurface: string;
}

const N = accentPalette;

const darkSurface = {
  dim: '#28262c',
  containerLowest: '#252429',
  base: '#2c2a30',
  containerLow: '#312f36',
  container: '#37353c',
  containerHigh: '#413f47',
  containerHighest: '#4c4952',
  bright: '#56535c',
};

function buildScheme(brand: Brand, dark: boolean): Md3Scheme {
  const P = RAMPS[brand];
  return dark
    ? {
        primary: P[300],
        primaryLight: P[200],
        primaryDark: P[400],
        onPrimary: P[900],
        primaryContainer: P[900],
        onPrimaryContainer: P[100],
        secondary: N[300],
        onSecondary: N[900],
        secondaryContainer: N[700],
        onSecondaryContainer: N[100],
        surface: darkSurface.base,
        onSurface: '#E6E1E9',
        onSurfaceVariant: '#CAC4D0',
        surfaceContainerLowest: darkSurface.containerLowest,
        surfaceContainerLow: darkSurface.containerLow,
        surfaceContainer: darkSurface.container,
        surfaceContainerHigh: darkSurface.containerHigh,
        surfaceContainerHighest: darkSurface.containerHighest,
        surfaceDim: darkSurface.dim,
        surfaceBright: darkSurface.bright,
        outline: '#939099',
        outlineVariant: '#56535c',
        inverseSurface: '#E6E1E9',
        inverseOnSurface: '#322F35',
      }
    : {
        primary: P[500],
        primaryLight: P[300],
        primaryDark: P[700],
        onPrimary: '#0d0c2c',
        primaryContainer: P[50],
        onPrimaryContainer: P[900],
        secondary: N[500],
        onSecondary: '#ffffff',
        secondaryContainer: N[100],
        onSecondaryContainer: N[900],
        surface: '#f4f2f1',
        onSurface: '#0d0c2c',
        onSurfaceVariant: '#6e6d80',
        surfaceContainerLowest: '#ffffff',
        surfaceContainerLow: '#f8f7f7',
        surfaceContainer: N[100],
        surfaceContainerHigh: N[200],
        surfaceContainerHighest: N[300],
        surfaceDim: N[200],
        surfaceBright: '#ffffff',
        outline: N[400],
        outlineVariant: N[200],
        inverseSurface: N[700],
        inverseOnSurface: '#ffffff',
      };
}

export const md3Schemes: Record<Brand, Record<Md3Mode, Md3Scheme>> = {
  dfrnt: { light: buildScheme('dfrnt', false), dark: buildScheme('dfrnt', true) },
  urgent: { light: buildScheme('urgent', false), dark: buildScheme('urgent', true) },
};

export function getMd3Scheme(_isUsCustomer: boolean, mode: Md3Mode): Md3Scheme {
  return md3Schemes.dfrnt[mode];
}

export function contrastRatio(fgHex: string, bgHex: string): number {
  const toLinear = (c: number): number => {
    const s = c / 255;
    return s <= 0.03928 ? s / 12.92 : ((s + 0.055) / 1.055) ** 2.4;
  };
  const lum = (hex: string): number => {
    const n = parseInt(hex.replace('#', ''), 16);
    const r = toLinear((n >> 16) & 0xff);
    const g = toLinear((n >> 8) & 0xff);
    const b = toLinear(n & 0xff);
    return 0.2126 * r + 0.7152 * g + 0.0722 * b;
  };
  const l1 = lum(fgHex);
  const l2 = lum(bgHex);
  const [hi, lo] = l1 > l2 ? [l1, l2] : [l2, l1];
  return (hi + 0.05) / (lo + 0.05);
}
