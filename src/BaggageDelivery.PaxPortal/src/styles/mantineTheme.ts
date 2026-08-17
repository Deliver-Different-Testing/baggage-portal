/**
 * DFRNT Mantine theme — the single source of truth for the brand. Portable copy
 * from the IntegrationManager AdminPortal (src/styles/mantineTheme.ts). Only
 * dependency is `@mantine/core` and the sibling `./md3`.
 *
 * Built 1:1 from the Feb 2026 DFRNT brand guidelines:
 *   Ink Blue  #0d0c2c  — shell (AppBar + sidebar) in both modes, dark-mode page bg
 *   Cyan      #3bc7f4  — primary interactive (buttons, links, active nav, focus)
 *   Light Grey#f4f2f1  — light-mode page background
 * Secondary accents: Reflex Blue #2a4eff, Purple #824ae0, Green #13b964 (success),
 * Orange #fe811a (warning), Red #dc3246 (error).
 *
 * Typography: Plus Jakarta Sans for UI and display headings. Lozenge (pill) buttons.
 */
import {
  createTheme,
  v8CssVariablesResolver,
  type CSSVariablesResolver,
  type MantineColorsTuple,
} from '@mantine/core';
import { getMd3Scheme } from './md3';

// ---------------------------------------------------------------------------
// Brand ramps (0 lightest → 9 darkest). Light ends use the guideline tints
// (20% / 40%); dark ends are hand-tuned. Regenerate precisely with
// @mantine/colors-generator from the six seed hexes if needed.
// ---------------------------------------------------------------------------
const brand: MantineColorsTuple = [
  '#e7f8fe', '#d8f4fd', '#b1e9fb', '#82dcf8', '#5bd1f5',
  '#3bc7f4', '#1eb2e6', '#1590c0', '#0f6f96', '#0a4d69',
]; // Cyan — primary

const ink: MantineColorsTuple = [
  '#ecebf1', '#cfced5', '#a8a7b6', '#83829a', '#6e6d80',
  '#4f4e66', '#35334f', '#211f40', '#141233', '#0d0c2c',
]; // Ink Blue — shell / neutral-dark

const reflex: MantineColorsTuple = [
  '#eaeeff', '#d4dcff', '#aab8ff', '#7d92ff', '#5670ff',
  '#2a4eff', '#233fd6', '#1b31a8', '#132279', '#0b134a',
]; // #2a4eff — info / links

const grape: MantineColorsTuple = [
  '#f3edfc', '#e6dbf9', '#cdb7f3', '#b088ec', '#9865e5',
  '#824ae0', '#6c3bbe', '#552e95', '#3f226e', '#291546',
]; // #824ae0 — accent

const green: MantineColorsTuple = [
  '#e5f8ee', '#d0f1e0', '#a1e3c1', '#5fd199', '#2ec27a',
  '#13b964', '#0f9d55', '#0b7d44', '#085e33', '#053f22',
]; // #13b964 — success

const orange: MantineColorsTuple = [
  '#fff3e6', '#ffe6d1', '#ffcda3', '#ffab63', '#fe9333',
  '#fe811a', '#e06d10', '#b3560b', '#853f08', '#582905',
]; // #fe811a — warning

const red: MantineColorsTuple = [
  '#fdeaec', '#f8d6da', '#f1adb5', '#e97b88', '#e25062',
  '#dc3246', '#bb2a3c', '#93212f', '#6c1823', '#460f16',
]; // #dc3246 — error

// A warm-grey neutral ramp for the light surfaces (Light Grey #f4f2f1 family).
const gray: MantineColorsTuple = [
  '#ffffff', '#f8f7f7', '#f4f2f1', '#e7e5e4', '#d6d3d1',
  '#a8a29e', '#78716c', '#57534e', '#44403c', '#292524',
];

// Mantine's `dark` tuple drives every default surface/text role in dark mode
// (`--mantine-color-body` = dark[7], default component bg = dark[6], border = dark[4],
// text = dark[0], dimmed = dark[2]). We map it 1:1 onto the DFRNT charcoal ladder in
// md3.ts so raw Mantine roles resolve to our tones instead of Mantine's generic charcoal.
const D = getMd3Scheme(true, 'dark');
const dark: MantineColorsTuple = [
  D.onSurface,                // 0 text          #E6E1E9
  D.onSurfaceVariant,         // 1               #CAC4D0
  D.outline,                  // 2 dimmed        #939099
  '#6f6c75',                  // 3 (interpolated between outline and outline-variant)
  D.outlineVariant,           // 4 border        #56535c
  D.surfaceContainerHighest,  // 5 hover         #4c4952
  D.surfaceContainerHigh,     // 6 default bg     #413f47
  D.surface,                  // 7 body / page   #2c2a30
  D.surfaceDim,               // 8               #28262c
  D.surfaceContainerLowest,   // 9 deepest       #252429
];

// ---------------------------------------------------------------------------
// DFRNT design tokens — framework-free constants consumed across the app.
// ---------------------------------------------------------------------------

/** Fixed Ink-Blue shell (AppBar + sidebar) — brand navy in both colour modes. */
export const sidebarColors = {
  appBar: ink[9],           // #0d0c2c
  bg: ink[8],               // #141233 — drawer panel, one tier lighter than the bar
  border: 'rgba(255, 255, 255, 0.10)',
  textPrimary: 'rgba(255, 255, 255, 0.95)',
  textSecondary: 'rgba(255, 255, 255, 0.60)',
  textMuted: 'rgba(255, 255, 255, 0.38)',
  hoverBg: 'rgba(255, 255, 255, 0.08)',
};

/** White-based overlays for content on the fixed Ink-Blue scrim (hero, shell, dialog headers). */
export const onBrandScrim = {
  /** The Ink-Blue hero itself — the ground every alpha below is mixed against. */
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

/** Theme-aware palette for code / terminal blocks (on Ink Blue). */
export const codeBlockPalette = {
  background: '#141233',
  backgroundError: '#2e1a1a',
  textSuccess: '#5fd199',
  textInfo: '#7d92ff',
  textError: '#f1adb5',
};

/**
 * Radius / type / duration / shadow tokens (MD3 corner scale).
 *
 * The radius scale is semantic, not decorative: `full` and `sm`–`lg` are for things
 * the passenger *touches*, `tile` (2px) is for things that are *printed* — the file
 * reference, the chosen window, the ETA, a docket line. Keeping the two apart is what
 * makes the baggage-tag motif read as a system rather than a one-off ornament.
 */
export const tokens = {
  radius: { xs: 4, sm: 8, md: 12, lg: 16, xl: 28, full: 9999, tile: 2 },
  /**
   * The display scale. `hero`/`figure` replace the near-identical clamps that had
   * drifted across the hero, the ETA card and the confirmed screen; `eyebrow` is the
   * single spec for the uppercase micro-label role, which had six.
   */
  type: {
    hero: 'clamp(32px, 8vw, 40px)',
    figure: 'clamp(24px, 6vw, 28px)',
    // `as const` so `textTransform` keeps its literal type and the object can be
    // spread straight into a `CSSProperties`.
    eyebrow: {
      fontSize: 10,
      fontWeight: 700,
      letterSpacing: '0.12em',
      textTransform: 'uppercase',
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

/** Back-compat brand token object (semantic hex, for the rare non-Mantine consumer). */
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

// ---------------------------------------------------------------------------
// theme.other typing — access via `theme.other.shell` / `.scrim` / `.tokens` etc.
// ---------------------------------------------------------------------------
declare module '@mantine/core' {
  export interface MantineThemeOther {
    shell: typeof sidebarColors;
    scrim: typeof onBrandScrim;
    code: typeof codeBlockPalette;
    tokens: typeof tokens;
  }
}

// ---------------------------------------------------------------------------
// The theme
// ---------------------------------------------------------------------------
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
    Button: { defaultProps: { radius: 9999 } }, // lozenge
    ActionIcon: { defaultProps: { radius: 9999 } },
    // Cards/paper sit one tonal tier above the page in both schemes (elevation by
    // tone, not shadow) — the surface vars flip per colour-scheme via the resolver
    // below. Applied as a class-based `styles.root`, NOT a `bg` defaultProp, so a
    // component's own inline `style={{ background }}` still wins — hero cards paint
    // a dark Ink scrim that way and must not be overpainted white in light mode.
    //
    // The hairline carries the separation the tone step can't: in light mode a card
    // is #ffffff on a #f4f2f1 page, which is a ~3% step and reads as no edge at all.
    // A flat outline suits the no-gradient house style better than a drop shadow.
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
      defaultProps: { radius: 'lg', centered: true },
      styles: { content: { backgroundColor: 'var(--dd-surface-container-high)' } },
    },
    // The tick and the dot are Ink on cyan, stated rather than inferred. Cyan is a
    // light colour (relative luminance ~0.48) — a white glyph on it is about 1.9:1
    // and effectively invisible at checkbox size.
    Checkbox: { defaultProps: { iconColor: 'var(--dd-on-brand-fill)' } },
    Radio: { defaultProps: { iconColor: 'var(--dd-on-brand-fill)' } },
    TextInput: { defaultProps: { radius: 'sm' } },
    Textarea: { defaultProps: { radius: 'sm' } },
    Select: { defaultProps: { radius: 'sm' } },
    Autocomplete: { defaultProps: { radius: 'sm' } },
    Menu: {
      defaultProps: { radius: 'sm', shadow: 'md' },
      styles: { dropdown: { backgroundColor: 'var(--dd-surface-container-high)' } },
    },
    Tooltip: { defaultProps: { radius: 'sm', color: 'ink' } },
    Badge: { defaultProps: { radius: 'sm' } },
    // Thicker tab targets than Mantine's thin default — comfortable, easier to hit.
    Tabs: { styles: { tab: { paddingBlock: 14, fontWeight: 500 } } },
  },
  other: {
    shell: sidebarColors,
    scrim: onBrandScrim,
    code: codeBlockPalette,
    tokens,
  },
});

/**
 * Bridges the DFRNT surface ladder (md3.ts, the single source of truth) into Mantine's
 * CSS variables per colour-scheme. Passed to `<MantineProvider cssVariablesResolver>`.
 *
 * `--mantine-color-body` sets the page/`AppShell.Main` background; the `--dd-surface-*`
 * custom vars back the Card/Paper/Menu/Modal surface defaults declared above, so a single
 * scheme flip repaints page → cards → menus with the intended tones.
 *
 * Layered on top of `v8CssVariablesResolver`: Mantine 9 made `variant="light"` fills
 * solid, and the DFRNT look wants the 8.x translucent tint (flat alpha, per the brand
 * rules) on light-variant surfaces such as the Alerts in PaxMobile.
 */
export const dfrntCssVariablesResolver: CSSVariablesResolver = (theme) => {
  const light = getMd3Scheme(true, 'light');
  const dk = getMd3Scheme(true, 'dark');
  const v8 = v8CssVariablesResolver(theme);
  return {
    variables: { ...v8.variables },
    light: {
      ...v8.light,
      '--mantine-color-body': light.surface, // #f4f2f1 page
      '--dd-surface': light.surface,
      '--dd-surface-container': light.surfaceContainerLowest, // #ffffff cards
      '--dd-surface-container-high': light.surfaceContainerLowest, // #ffffff menus/dialogs
      '--dd-outline-variant': light.outlineVariant, // #e7e5e4 — card + docket hairlines
      // Content sitting ON brand cyan. Both are stated explicitly rather than left to
      // Mantine's `-contrast` / `-light-color` vars: under v8CssVariablesResolver
      // `--mantine-color-brand-light-color` resolves to brand[5] — the same cyan as
      // the tint behind it — so anything relying on it was cyan-on-cyan.
      '--dd-on-brand-fill': ink[9], // #0d0c2c on the solid cyan fill (~8:1)
      '--dd-on-brand-tint': brand[8], // #0f6f96 on the pale cyan tint (~5:1)
      // Disabled inputs (e.g. saved delivery address) read as a locked summary,
      // not greyed-out placeholder text — keep the value legible. See index.css
      // for the paired opacity/-webkit-text-fill-color override.
      '--mantine-color-disabled-color': gray[7], // #57534e — readable locked text
    },
    dark: {
      ...v8.dark,
      '--mantine-color-body': dk.surface, // #2c2a30 page
      '--dd-surface': dk.surface,
      '--dd-surface-container': dk.surfaceContainer, // #37353c cards
      '--dd-surface-container-high': dk.surfaceContainerHigh, // #413f47 menus/dialogs
      '--dd-outline-variant': dk.outlineVariant, // #56535c — card + docket hairlines
      // Cyan stays a light colour in dark mode (the fill steps to brand[4]), so the
      // fill still takes Ink content; only the tint flips, because there the ground
      // is translucent cyan over charcoal.
      '--dd-on-brand-fill': ink[9], // #0d0c2c on the solid cyan fill
      '--dd-on-brand-tint': brand[2], // #b1e9fb on cyan-over-charcoal (~7:1)
      // Mantine's dark defaults are near-invisible on the charcoal ladder:
      // error = red[8] (#6c1823) and disabled text = dark[3] (#6f6c75) at 0.6
      // opacity. Lift both so validation messages and locked field values read.
      '--mantine-color-error': red[3], // #e97b88 — legible error red on charcoal
      '--mantine-color-disabled': dk.surfaceDim, // #28262c — inset locked field
      '--mantine-color-disabled-color': dk.onSurfaceVariant, // #cac4d0 — readable
    },
  };
};
