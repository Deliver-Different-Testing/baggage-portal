// Tabler and Lucide ship a single barrel .d.ts, not per-icon declarations, so the
// deep-path .mjs imports used in `src/components/Icon.tsx` (to keep the 6169-entry
// barrel out of the build graph) have no types. Declare them as generic icon
// components — Icon.tsx already treats every glyph as ComponentType<Record<string, unknown>>.
declare module '@tabler/icons-react/dist/esm/icons/*.mjs' {
  import type { ComponentType } from 'react'
  const Icon: ComponentType<Record<string, unknown>>
  export default Icon
}

declare module 'lucide-react/dist/esm/icons/*.mjs' {
  import type { ComponentType } from 'react'
  const Icon: ComponentType<Record<string, unknown>>
  export default Icon
}
