import { defineConfig, minimal2023Preset } from '@vite-pwa/assets-generator/config'

// Maskable + apple icons default to a solid white background; force them
// transparent so every generated icon is just the glyph, matching the
// DespatchWeb brand favicon (no background shape).
const transparent = { r: 0, g: 0, b: 0, alpha: 0 }

export default defineConfig({
  headLinkOptions: { preset: '2023' },
  preset: {
    ...minimal2023Preset,
    maskable: {
      ...minimal2023Preset.maskable,
      resizeOptions: { background: transparent },
    },
    apple: {
      ...minimal2023Preset.apple,
      resizeOptions: { background: transparent },
    },
  },
  images: ['public/favicon.svg'],
})
