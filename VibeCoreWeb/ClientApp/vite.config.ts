import { defineConfig } from "vite";
import react from "@vitejs/plugin-react";
import tailwindcss from "@tailwindcss/vite";

const previewUrl = process.env.PREVIEW_URL
  ? new URL(process.env.PREVIEW_URL)
  : undefined;

const configuredHmrHost =
  process.env.VITE_HMR_HOST ?? previewUrl?.hostname;
const hmrProtocol =
  process.env.VITE_HMR_PROTOCOL ??
  (previewUrl
    ? previewUrl.protocol === "https:"
      ? "wss"
      : "ws"
    : undefined);
const vitePort = Number.parseInt(process.env.VITE_PORT ?? "5173", 10);
const configuredClientPort = Number.parseInt(
  process.env.VITE_HMR_CLIENT_PORT ?? "",
  10,
);
const hmrClientPort = configuredClientPort || undefined;

export default defineConfig({
  appType: "custom",
  cacheDir: process.env.VIBECORE_VITE_CACHE_DIR ?? "node_modules/.vite",
  plugins: [tailwindcss(), react()],
  server: {
    origin: previewUrl?.origin,
    host: "0.0.0.0",
    port: Number.isNaN(vitePort) ? 5173 : vitePort,
    strictPort: true,
    cors: true,
    hmr: {
      path: "/__vite_hmr",
      ...(hmrProtocol ? { protocol: hmrProtocol } : {}),
      ...(configuredHmrHost ? { host: configuredHmrHost } : {}),
      ...(hmrClientPort ? { clientPort: hmrClientPort } : {}),
    },
  },
  base: "/app/",
  build: {
    outDir: "../wwwroot/app",
    emptyOutDir: true,
    manifest: ".vite/manifest.json",
    rollupOptions: {
      input: ["./src/main.tsx", "./tailwind.config.css"],
    },
  },
});
