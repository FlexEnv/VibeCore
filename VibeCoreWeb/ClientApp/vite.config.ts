import { defineConfig } from "vite";
import react from "@vitejs/plugin-react";
import tailwindcss from "@tailwindcss/vite";

const rawPreviewUrl =
  process.env.PREVIEW_URL ??
  process.env.VITE_PUBLIC_URL ??
  process.env.PUBLIC_PREVIEW_URL ??
  "";

let previewUrl: URL | undefined;
if (rawPreviewUrl) {
  try {
    previewUrl = new URL(rawPreviewUrl);
  } catch {
    previewUrl = undefined;
  }
}

const configuredHmrHost =
  process.env.VITE_HMR_HOST ??
  process.env.HMR_HOST ??
  previewUrl?.host ??
  "localhost";
const hmrHost = configuredHmrHost
  .replace(/^https?:\/\//, "")
  .replace(/\/+$/, "");
const hmrProtocol =
  process.env.VITE_HMR_PROTOCOL ??
  process.env.HMR_PROTOCOL ??
  (previewUrl?.protocol === "https:" ? "wss" : "ws");
const vitePort = Number.parseInt(process.env.VITE_PORT ?? "5173", 10);
const aspNetPort = Number.parseInt(
  process.env.ASPNETCORE_HTTP_PORT ?? process.env.ASPNET_PORT ?? "3000",
  10,
);
const configuredClientPort = Number.parseInt(
  process.env.VITE_HMR_CLIENT_PORT ?? process.env.HMR_CLIENT_PORT ?? "",
  10,
);
const hmrClientPort =
  configuredClientPort ||
  (previewUrl ? undefined : aspNetPort);

export default defineConfig({
  appType: "custom",
  plugins: [tailwindcss(), react()],
  server: {
    origin: previewUrl?.origin,
    host: "0.0.0.0",
    port: Number.isNaN(vitePort) ? 5173 : vitePort,
    strictPort: true,
    cors: true,
    hmr: {
      protocol: hmrProtocol,
      host: hmrHost,
      clientPort: hmrClientPort,
      path: "/__vite_hmr",
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
