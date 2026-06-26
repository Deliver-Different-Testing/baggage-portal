// Airline branding keyed by airline (client) CODE — never the free-text display
// name. The passenger header/primary palette is driven from this map. Unknown or
// missing codes fall back to DEFAULT (the dfrnt blue), so the page never crashes
// on an unrecognized airline.

export type AirlineBrand = {
  primary: string
  primaryDark: string
  primaryLight: string
  contrastText: string
}

const DEFAULT: AirlineBrand = {
  primary: '#2196f3',
  primaryDark: '#1976d2',
  primaryLight: '#64b5f6',
  contrastText: '#FFFFFF',
}

// Keyed by IATA 2-letter airline code (matches the example codes in the brief).
// Colours are brand-approximate solids — no gradients, per the house style.
const AIRLINE_BRANDS: Record<string, AirlineBrand> = {
  // --- New Zealand ---
  NZ: {
    // Air New Zealand — Pacific teal
    primary: '#008c95',
    primaryDark: '#006770',
    primaryLight: '#4db6bc',
    contrastText: '#FFFFFF',
  },

  // --- Australia ---
  QF: {
    // Qantas — red
    primary: '#e0001b',
    primaryDark: '#a30014',
    primaryLight: '#ff5a4d',
    contrastText: '#FFFFFF',
  },
  VA: {
    // Virgin Australia — red
    primary: '#e10600',
    primaryDark: '#a8000a',
    primaryLight: '#ff5b52',
    contrastText: '#FFFFFF',
  },
  JQ: {
    // Jetstar — orange (also flies NZ domestic)
    primary: '#ec5402',
    primaryDark: '#b33d00',
    primaryLight: '#ff8a4d',
    contrastText: '#FFFFFF',
  },
  ZL: {
    // Rex (Regional Express) — orange
    primary: '#e35205',
    primaryDark: '#ab3d00',
    primaryLight: '#ff7d3a',
    contrastText: '#FFFFFF',
  },

  // --- United States ---
  AA: {
    // American Airlines — blue
    primary: '#0078d2',
    primaryDark: '#00558f',
    primaryLight: '#4da8e8',
    contrastText: '#FFFFFF',
  },
  DL: {
    // Delta — navy
    primary: '#003a70',
    primaryDark: '#00264d',
    primaryLight: '#3a6ea5',
    contrastText: '#FFFFFF',
  },
  UA: {
    // United — blue
    primary: '#0033a0',
    primaryDark: '#00226b',
    primaryLight: '#4d76c4',
    contrastText: '#FFFFFF',
  },
  WN: {
    // Southwest — blue
    primary: '#304cb2',
    primaryDark: '#1f3585',
    primaryLight: '#6b80d6',
    contrastText: '#FFFFFF',
  },
  B6: {
    // JetBlue — navy
    primary: '#003876',
    primaryDark: '#00264f',
    primaryLight: '#3a6aa5',
    contrastText: '#FFFFFF',
  },
  AS: {
    // Alaska Airlines — midnight blue
    primary: '#01426a',
    primaryDark: '#002c47',
    primaryLight: '#3a6e94',
    contrastText: '#FFFFFF',
  },
  F9: {
    // Frontier — green
    primary: '#00843d',
    primaryDark: '#005c2a',
    primaryLight: '#4db680',
    contrastText: '#FFFFFF',
  },
  G4: {
    // Allegiant — blue
    primary: '#003da5',
    primaryDark: '#002b73',
    primaryLight: '#3a6ec4',
    contrastText: '#FFFFFF',
  },
  NK: {
    // Spirit — bright yellow; needs dark contrast text
    primary: '#fff200',
    primaryDark: '#ccc100',
    primaryLight: '#fff64d',
    contrastText: 'rgba(0, 0, 0, 0.87)',
  },

  // --- Asia / trans-Pacific ---
  SQ: {
    // Singapore Airlines — navy
    primary: '#1d3c6e',
    primaryDark: '#122a4f',
    primaryLight: '#4f6a9c',
    contrastText: '#FFFFFF',
  },
  NH: {
    // All Nippon Airways (ANA) — Triton blue
    primary: '#00188f',
    primaryDark: '#001066',
    primaryLight: '#4d6ec4',
    contrastText: '#FFFFFF',
  },
  JL: {
    // Japan Airlines — red
    primary: '#c8102e',
    primaryDark: '#960c22',
    primaryLight: '#e85a6e',
    contrastText: '#FFFFFF',
  },
  KE: {
    // Korean Air — blue
    primary: '#00529b',
    primaryDark: '#003a6e',
    primaryLight: '#4d8ac4',
    contrastText: '#FFFFFF',
  },
  CI: {
    // China Airlines — red
    primary: '#d11f33',
    primaryDark: '#9c1626',
    primaryLight: '#e8616f',
    contrastText: '#FFFFFF',
  },
  BR: {
    // EVA Air — green
    primary: '#1f7a44',
    primaryDark: '#14562f',
    primaryLight: '#4dab74',
    contrastText: '#FFFFFF',
  },
  TG: {
    // Thai Airways — brand is purple; substituted brand magenta to respect the
    // no-purple/indigo house rule
    primary: '#c8006b',
    primaryDark: '#94004f',
    primaryLight: '#e34d97',
    contrastText: '#FFFFFF',
  },
  MH: {
    // Malaysia Airlines — blue
    primary: '#00529f',
    primaryDark: '#003a70',
    primaryLight: '#4d8ac4',
    contrastText: '#FFFFFF',
  },
  PR: {
    // Philippine Airlines — navy
    primary: '#1b3f8b',
    primaryDark: '#122a5e',
    primaryLight: '#4f6eb0',
    contrastText: '#FFFFFF',
  },
  VN: {
    // Vietnam Airlines — blue
    primary: '#0e4d8c',
    primaryDark: '#093461',
    primaryLight: '#4d80b5',
    contrastText: '#FFFFFF',
  },
  GA: {
    // Garuda Indonesia — sky blue
    primary: '#0a7abf',
    primaryDark: '#055a8e',
    primaryLight: '#4da6d9',
    contrastText: '#FFFFFF',
  },
  CX: {
    // Cathay Pacific — teal/green
    primary: '#006564',
    primaryDark: '#00403f',
    primaryLight: '#4d9897',
    contrastText: '#FFFFFF',
  },

  // --- Middle East ---
  EK: {
    // Emirates — red
    primary: '#d71921',
    primaryDark: '#a3121a',
    primaryLight: '#ff5a4d',
    contrastText: '#FFFFFF',
  },
  QR: {
    // Qatar Airways — burgundy
    primary: '#5c0632',
    primaryDark: '#3d041f',
    primaryLight: '#8a3a5e',
    contrastText: '#FFFFFF',
  },
  EY: {
    // Etihad Airways — gold
    primary: '#ad841f',
    primaryDark: '#7d5f14',
    primaryLight: '#d4ab4d',
    contrastText: '#FFFFFF',
  },

  // --- Pacific ---
  FJ: {
    // Fiji Airways — turquoise blue
    primary: '#0090b3',
    primaryDark: '#006580',
    primaryLight: '#4db6cf',
    contrastText: '#FFFFFF',
  },

  // --- Americas (non-US) ---
  LA: {
    // LATAM — brand is indigo; substituted navy to respect the
    // no-purple/indigo house rule
    primary: '#1c2b5e',
    primaryDark: '#111c40',
    primaryLight: '#4f5e94',
    contrastText: '#FFFFFF',
  },
  AC: {
    // Air Canada — red
    primary: '#d52b1e',
    primaryDark: '#a3160c',
    primaryLight: '#f0584d',
    contrastText: '#FFFFFF',
  },

  // --- US (additional) ---
  HA: {
    // Hawaiian Airlines — brand is purple; substituted magenta to respect the
    // no-purple/indigo house rule
    primary: '#a3197a',
    primaryDark: '#7a1259',
    primaryLight: '#c44d9e',
    contrastText: '#FFFFFF',
  },

  // --- Europe ---
  BA: {
    // British Airways — blue
    primary: '#075aaa',
    primaryDark: '#034178',
    primaryLight: '#4d8cc9',
    contrastText: '#FFFFFF',
  },
  LH: {
    // Lufthansa — dark blue
    primary: '#05164d',
    primaryDark: '#030d2e',
    primaryLight: '#3a4a85',
    contrastText: '#FFFFFF',
  },
}

export function getAirlineBrand(airlineCode?: string | null): AirlineBrand {
  if (!airlineCode) return DEFAULT
  return AIRLINE_BRANDS[airlineCode.trim().toUpperCase()] ?? DEFAULT
}
