#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
CLIENT_DIR="$ROOT_DIR/VibeCoreWeb/ClientApp"
DEV_PORT="${DEV_PORT:-3000}"

export ASPNETCORE_ENVIRONMENT="${ASPNETCORE_ENVIRONMENT:-Development}"
export ASPNETCORE_URLS="http://0.0.0.0:${DEV_PORT}"
export DOTNET_WATCH_SUPPRESS_LAUNCH_BROWSER=1

cleanup() {
  kill "${VITE_PID:-}" "${DOTNET_PID:-}" 2>/dev/null || true
  wait "${VITE_PID:-}" "${DOTNET_PID:-}" 2>/dev/null || true
}
trap cleanup EXIT INT TERM

cd "$CLIENT_DIR"
npm run dev -- --host 127.0.0.1 &
VITE_PID=$!

cd "$ROOT_DIR"
dotnet watch \
  --project VibeCoreWeb/VibeCoreWeb.csproj \
  --non-interactive \
  run --no-restore --urls "$ASPNETCORE_URLS" &
DOTNET_PID=$!

while kill -0 "$VITE_PID" 2>/dev/null && kill -0 "$DOTNET_PID" 2>/dev/null; do
  sleep 1
done

wait "$VITE_PID" "$DOTNET_PID"
