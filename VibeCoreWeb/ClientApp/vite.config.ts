import { defineConfig } from "vite";
import react from "@vitejs/plugin-react";
import tailwindcss from "@tailwindcss/vite";

// https://vite.dev/config/
export default defineConfig(() => {
  const previewUrl = process.env.PREVIEW_URL
    ? new URL(process.env.PREVIEW_URL)
    : undefined;

  return {
  appType: "custom",
  plugins: [tailwindcss(), react()],
  server: {
    port: 5173,
    strictPort: true,
    hmr: {
      protocol: previewUrl?.protocol === "https:" ? "wss" : "ws",
      host: previewUrl?.hostname ?? "localhost",
      clientPort: previewUrl?.protocol === "https:" ? 443 : undefined,
    },
    allowedHosts: [".flexenv.ai"],
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
  };
});
