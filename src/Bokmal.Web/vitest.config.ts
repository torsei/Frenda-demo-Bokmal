import { defineConfig } from 'vitest/config';
import react from '@vitejs/plugin-react';
import path from 'node:path';

export default defineConfig({
  plugins: [react()],
  test: {
    environment: 'jsdom',
    setupFiles: ['./vitest.setup.ts'],
    // Only the presentation logic that encodes a decision is worth testing here. Server
    // Components fetching data, and the layout rendering, are the framework's job.
    include: ['**/*.test.ts', '**/*.test.tsx'],
    exclude: ['node_modules/**', '.next/**', 'generated/**'],
  },
  resolve: {
    alias: { '@': path.resolve(__dirname, '.') },
  },
});
