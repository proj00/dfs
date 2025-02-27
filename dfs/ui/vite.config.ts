import { defineConfig } from "vite";
import plugin from "@vitejs/plugin-react";

// https://vitejs.dev/config/
export default defineConfig({
  plugins: [plugin()],
  server: {
    port: 59102,
  },
  build: {
    outDir: "./../node/UiResources",
    emptyOutDir: true,
  },
});
