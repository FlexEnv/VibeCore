#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
CLIENT_DIR="$ROOT_DIR/VibeCoreWeb/ClientApp"
DEV_PORT="${DEV_PORT:-3000}"

export ASPNETCORE_ENVIRONMENT="${ASPNETCORE_ENVIRONMENT:-Development}"
export ASPNETCORE_URLS="http://0.0.0.0:${DEV_PORT}"
export DOTNET_WATCH_SUPPRESS_LAUNCH_BROWSER=1
export DOTNET_WATCH_SUPPRESS_BROWSER_REFRESH=1
export VIBECORE_VITE_CACHE_DIR="${VIBECORE_VITE_CACHE_DIR:-/tmp/vibecore-vite-cache}"

cleanup() {
  kill "${VITE_PID:-}" "${DOTNET_PID:-}" 2>/dev/null || true
  wait "${VITE_PID:-}" "${DOTNET_PID:-}" 2>/dev/null || true
}
trap cleanup EXIT INT TERM

cd "$CLIENT_DIR"
npm run dev -- --host 127.0.0.1 --configLoader runner &
VITE_PID=$!

cd "$ROOT_DIR"
dotnet watch \
  --project VibeCoreWeb/VibeCoreWeb.csproj \
  --no-hot-reload \
  --non-interactive \
  run --urls "$ASPNETCORE_URLS" &
DOTNET_PID=$!

while kill -0 "$VITE_PID" 2>/dev/null && kill -0 "$DOTNET_PID" 2>/dev/null; do
  sleep 1
done

wait "$VITE_PID" "$DOTNET_PID"
