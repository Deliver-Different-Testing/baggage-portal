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

const AIRLINE_BRANDS: Record<string, AirlineBrand> = {
  NZ: {
    primary: '#008c95',
    primaryDark: '#006770',
    primaryLight: '#4db6bc',
    contrastText: '#FFFFFF',
  },

  QF: {
    primary: '#e0001b',
    primaryDark: '#a30014',
    primaryLight: '#ff5a4d',
    contrastText: '#FFFFFF',
  },
  VA: {
    primary: '#e10600',
    primaryDark: '#a8000a',
    primaryLight: '#ff5b52',
    contrastText: '#FFFFFF',
  },
  JQ: {
    primary: '#ec5402',
    primaryDark: '#b33d00',
    primaryLight: '#ff8a4d',
    contrastText: '#FFFFFF',
  },
  ZL: {
    primary: '#e35205',
    primaryDark: '#ab3d00',
    primaryLight: '#ff7d3a',
    contrastText: '#FFFFFF',
  },

  AA: {
    primary: '#0078d2',
    primaryDark: '#00558f',
    primaryLight: '#4da8e8',
    contrastText: '#FFFFFF',
  },
  DL: {
    primary: '#003a70',
    primaryDark: '#00264d',
    primaryLight: '#3a6ea5',
    contrastText: '#FFFFFF',
  },
  UA: {
    primary: '#0033a0',
    primaryDark: '#00226b',
    primaryLight: '#4d76c4',
    contrastText: '#FFFFFF',
  },
  WN: {
    primary: '#304cb2',
    primaryDark: '#1f3585',
    primaryLight: '#6b80d6',
    contrastText: '#FFFFFF',
  },
  B6: {
    primary: '#003876',
    primaryDark: '#00264f',
    primaryLight: '#3a6aa5',
    contrastText: '#FFFFFF',
  },
  AS: {
    primary: '#01426a',
    primaryDark: '#002c47',
    primaryLight: '#3a6e94',
    contrastText: '#FFFFFF',
  },
  F9: {
    primary: '#00843d',
    primaryDark: '#005c2a',
    primaryLight: '#4db680',
    contrastText: '#FFFFFF',
  },
  G4: {
    primary: '#003da5',
    primaryDark: '#002b73',
    primaryLight: '#3a6ec4',
    contrastText: '#FFFFFF',
  },
  NK: {
    primary: '#fff200',
    primaryDark: '#ccc100',
    primaryLight: '#fff64d',
    contrastText: 'rgba(0, 0, 0, 0.87)',
  },

  SQ: {
    primary: '#1d3c6e',
    primaryDark: '#122a4f',
    primaryLight: '#4f6a9c',
    contrastText: '#FFFFFF',
  },
  NH: {
    primary: '#00188f',
    primaryDark: '#001066',
    primaryLight: '#4d6ec4',
    contrastText: '#FFFFFF',
  },
  JL: {
    primary: '#c8102e',
    primaryDark: '#960c22',
    primaryLight: '#e85a6e',
    contrastText: '#FFFFFF',
  },
  KE: {
    primary: '#00529b',
    primaryDark: '#003a6e',
    primaryLight: '#4d8ac4',
    contrastText: '#FFFFFF',
  },
  CI: {
    primary: '#d11f33',
    primaryDark: '#9c1626',
    primaryLight: '#e8616f',
    contrastText: '#FFFFFF',
  },
  BR: {
    primary: '#1f7a44',
    primaryDark: '#14562f',
    primaryLight: '#4dab74',
    contrastText: '#FFFFFF',
  },
  TG: {
    primary: '#c8006b',
    primaryDark: '#94004f',
    primaryLight: '#e34d97',
    contrastText: '#FFFFFF',
  },
  MH: {
    primary: '#00529f',
    primaryDark: '#003a70',
    primaryLight: '#4d8ac4',
    contrastText: '#FFFFFF',
  },
  PR: {
    primary: '#1b3f8b',
    primaryDark: '#122a5e',
    primaryLight: '#4f6eb0',
    contrastText: '#FFFFFF',
  },
  VN: {
    primary: '#0e4d8c',
    primaryDark: '#093461',
    primaryLight: '#4d80b5',
    contrastText: '#FFFFFF',
  },
  GA: {
    primary: '#0a7abf',
    primaryDark: '#055a8e',
    primaryLight: '#4da6d9',
    contrastText: '#FFFFFF',
  },
  CX: {
    primary: '#006564',
    primaryDark: '#00403f',
    primaryLight: '#4d9897',
    contrastText: '#FFFFFF',
  },

  EK: {
    primary: '#d71921',
    primaryDark: '#a3121a',
    primaryLight: '#ff5a4d',
    contrastText: '#FFFFFF',
  },
  QR: {
    primary: '#5c0632',
    primaryDark: '#3d041f',
    primaryLight: '#8a3a5e',
    contrastText: '#FFFFFF',
  },
  EY: {
    primary: '#ad841f',
    primaryDark: '#7d5f14',
    primaryLight: '#d4ab4d',
    contrastText: '#FFFFFF',
  },

  FJ: {
    primary: '#0090b3',
    primaryDark: '#006580',
    primaryLight: '#4db6cf',
    contrastText: '#FFFFFF',
  },

  LA: {
    primary: '#1c2b5e',
    primaryDark: '#111c40',
    primaryLight: '#4f5e94',
    contrastText: '#FFFFFF',
  },
  AC: {
    primary: '#d52b1e',
    primaryDark: '#a3160c',
    primaryLight: '#f0584d',
    contrastText: '#FFFFFF',
  },

  HA: {
    primary: '#a3197a',
    primaryDark: '#7a1259',
    primaryLight: '#c44d9e',
    contrastText: '#FFFFFF',
  },

  BA: {
    primary: '#075aaa',
    primaryDark: '#034178',
    primaryLight: '#4d8cc9',
    contrastText: '#FFFFFF',
  },
  LH: {
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
