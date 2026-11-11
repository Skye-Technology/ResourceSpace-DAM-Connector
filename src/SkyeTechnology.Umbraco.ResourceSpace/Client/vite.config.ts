import { defineConfig } from 'vite';

export default defineConfig({
  publicDir: 'public',
  build: {
    lib: {
      entry: 'src/index.ts',
      formats: ['es'],
      fileName: () => 'resourcespace-connector.js',
    },
    outDir: '../wwwroot/App_Plugins/ResourceSpace',
    emptyOutDir: true,
    sourcemap: true,
    rollupOptions: {
      external: [/^@umbraco/],
    },
  },
});
