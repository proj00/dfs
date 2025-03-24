import { defineConfig } from "vite";
import react from "@vitejs/plugin-react";
import path from "path";

// https://vitejs.dev/config/
export default defineConfig({
  plugins: [react()],
  server: {
    port: 59102,
  },
  build: {
    outDir: "./../node/UiResources",
    emptyOutDir: true,
  },
  base: "http://ui.resources/",
  resolve: {
    alias: {
      "@": path.resolve(__dirname, "./src"),
    },
  },
});
