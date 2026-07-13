import { defineConfig } from "vitest/config";
import react from "@vitejs/plugin-react";

export default defineConfig({
  plugins: [react()],
  server: {
    host: "127.0.0.1",
    port: 5173,
    strictPort: true,
  },
  preview: {
    host: "127.0.0.1",
    port: 4173,
    strictPort: true,
  },
  build: {
    // Three.js is isolated in a lazy chunk; keep the entry bundle small while
    // allowing the self-contained 3D runtime to remain cacheable.
    chunkSizeWarningLimit: 1000,
  },
  assetsInclude: ["**/*.task", "**/*.wasm", "**/*.glb"],
  test: {
    environment: "node",
    include: ["src/**/*.test.ts"],
    coverage: {
      reporter: ["text", "html"],
    },
  },
});
