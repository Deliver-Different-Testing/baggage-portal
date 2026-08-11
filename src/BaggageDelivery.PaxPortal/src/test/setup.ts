import '@testing-library/jest-dom/vitest'

// jsdom implements neither of these, but Mantine's MantineProvider reads
// window.matchMedia on mount and several components observe resize. Provide inert
// stubs so components render under the test environment.
if (typeof window !== 'undefined' && !window.matchMedia) {
  window.matchMedia = (query: string) =>
    ({
      matches: false,
      media: query,
      onchange: null,
      addListener: () => {},
      removeListener: () => {},
      addEventListener: () => {},
      removeEventListener: () => {},
      dispatchEvent: () => false,
    }) as unknown as MediaQueryList
}

// Mantine 9's autosizing Textarea re-measures on `document.fonts` "loadingdone";
// jsdom has no FontFaceSet at all, so the effect throws and takes the tree with it.
if (typeof document !== 'undefined' && !document.fonts) {
  Object.defineProperty(document, 'fonts', {
    configurable: true,
    value: {
      addEventListener: () => {},
      removeEventListener: () => {},
    },
  })
}

if (typeof globalThis.ResizeObserver === 'undefined') {
  globalThis.ResizeObserver = class {
    observe() {}
    unobserve() {}
    disconnect() {}
  }
}
