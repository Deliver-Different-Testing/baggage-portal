import { defineConfig } from 'vitest/config'
import react from '@vitejs/plugin-react'
import { VitePWA } from 'vite-plugin-pwa'

export default defineConfig({
  plugins: [
    react(),
    VitePWA({
      registerType: 'autoUpdate',
      includeAssets: ['favicon.svg', 'icons/icon-192.png', 'icons/icon-512.png'],
      manifest: {
        name: 'Urgent Baggage Delivery',
        short_name: 'Baggage',
        description: 'Confirm delivery details and track your baggage',
        theme_color: '#001F3D',
        background_color: '#001F3D',
        display: 'standalone',
        orientation: 'portrait',
        start_url: '/?source=pwa',
        icons: [
          { src: '/icons/icon-192.png', sizes: '192x192', type: 'image/png' },
          { src: '/icons/icon-512.png', sizes: '512x512', type: 'image/png' },
          { src: '/icons/icon-512-maskable.png', sizes: '512x512', type: 'image/png', purpose: 'maskable' },
        ],
      },
      workbox: {
        navigateFallback: '/index.html',
        navigateFallbackDenylist: [/^\/api\//],
        runtimeCaching: [
          {
            urlPattern: ({ url }) => url.pathname.startsWith('/api/v1/pax/booking'),
            handler: 'NetworkFirst',
            options: {
              cacheName: 'pax-booking',
              networkTimeoutSeconds: 4,
              expiration: { maxAgeSeconds: 60 * 30 },
            },
          },
          {
            urlPattern: ({ url }) => url.pathname.startsWith('/api/v1/pax/tracking'),
            handler: 'StaleWhileRevalidate',
            options: {
              cacheName: 'pax-tracking',
              expiration: { maxAgeSeconds: 60 * 60 },
            },
          },
        ],
      },
    }),
  ],
  build: {
    rollupOptions: {
      output: {
        manualChunks(id) {
          if (id.includes('node_modules')) {
            if (/[\\/]node_modules[\\/](react|react-dom|react-router|react-router-dom|scheduler)[\\/]/.test(id)) {
              return 'vendor-react'
            }
            if (/[\\/]node_modules[\\/](@mui|@emotion)[\\/]/.test(id)) {
              return 'vendor-mui'
            }
            if (/[\\/]node_modules[\\/](@tanstack[\\/]react-query|axios)[\\/]/.test(id)) {
              return 'vendor-query'
            }
          }
          return undefined
        },
      },
    },
  },
  server: {
    host: true,
    allowedHosts: ['baggagedelivery.local.deliverdifferent.com'],
    proxy: {
      '/api': {
        target: 'http://baggagedelivery.local.deliverdifferent.com:5298',
        changeOrigin: true,
      },
    },
  },
  test: {
    globals: true,
    environment: 'jsdom',
    setupFiles: './src/test/setup.ts',
    testTimeout: 30000,
    clearMocks: true,
    env: {
      VITE_API_URL: 'http://test-api.local/api/v1',
    },
  },
})
