import { defineConfig, Plugin } from 'vite';

/** Strip type="module" and crossorigin from script tags so the build works over file:// */
function stripModuleAttrs(): Plugin {
  return {
    name: 'strip-module-attrs',
    enforce: 'post',
    transformIndexHtml(html: string) {
      return html.replace(
        /<script\b[^>]*\btype="module"[^>]*\bcrossorigin\b[^>]*>/g,
        (tag) => tag.replace(' type="module"', '').replace(' crossorigin', ''),
      );
    },
  };
}

export default defineConfig({
  root: '.',
  base: './',
  plugins: [stripModuleAttrs()],
  server: {
    host: '127.0.0.1',
    port: 1420,
    strictPort: true,
  },
  build: {
    outDir: 'dist',
    emptyOutDir: true,
    rollupOptions: {
      output: {
        format: 'iife',
        entryFileNames: 'app.js',
        assetFileNames: '[name][extname]',
        inlineDynamicImports: true,
      },
    },
  },
});
