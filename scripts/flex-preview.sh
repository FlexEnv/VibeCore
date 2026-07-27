#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
CLIENT_DIR="$ROOT_DIR/VibeCoreWeb/ClientApp"
DEV_PORT="${DEV_PORT:-3000}"
STATE_DIR="${VIBECORE_PREVIEW_STATE_DIR:-/tmp/vibecore-preview-${UID}}"
PID_FILE="$STATE_DIR/flex-preview-${DEV_PORT}.pid"

export ASPNETCORE_ENVIRONMENT="${ASPNETCORE_ENVIRONMENT:-Development}"
export ASPNETCORE_URLS="http://0.0.0.0:${DEV_PORT}"
export DOTNET_WATCH_SUPPRESS_LAUNCH_BROWSER=1
export DOTNET_WATCH_SUPPRESS_BROWSER_REFRESH=1
export DOTNET_USE_POLLING_FILE_WATCHER="${DOTNET_USE_POLLING_FILE_WATCHER:-1}"
export CHOKIDAR_USEPOLLING="${CHOKIDAR_USEPOLLING:-true}"
export VIBECORE_VITE_CACHE_DIR="${VIBECORE_VITE_CACHE_DIR:-/tmp/vibecore-vite-cache}"

process_identity() {
  local process_pid="$1"
  if [[ -r "/proc/${process_pid}/stat" ]]; then
    awk '{ print $22 }' "/proc/${process_pid}/stat"
  else
    local started_at
    started_at="$(
      ps -p "$process_pid" -o lstart= 2>/dev/null |
        sed -e 's/^[[:space:]]*//' -e 's/[[:space:]]*$//'
    )"
    printf '%s\n' "${started_at:-unavailable}"
  fi
}

cleanup() {
  kill "${VITE_PID:-}" "${DOTNET_PID:-}" 2>/dev/null || true
  wait "${VITE_PID:-}" "${DOTNET_PID:-}" 2>/dev/null || true
  if [[ -f "$PID_FILE" ]]; then
    local recorded_pid
    IFS=$'\t' read -r recorded_pid _ < "$PID_FILE" || true
    if [[ "$recorded_pid" == "$$" ]]; then
      rm -f "$PID_FILE"
    fi
  fi
}

stop_preview() {
  if [[ ! -f "$PID_FILE" ]]; then
    echo "No Flex preview is running on port ${DEV_PORT}."
    return 0
  fi

  local preview_pid recorded_identity current_identity
  IFS=$'\t' read -r preview_pid recorded_identity < "$PID_FILE" || true
  if [[ ! "$preview_pid" =~ ^[1-9][0-9]*$ ]] ||
      [[ -z "$recorded_identity" ]]; then
    echo "Removing invalid Flex preview PID file: ${PID_FILE}" >&2
    rm -f "$PID_FILE"
    return 1
  fi

  current_identity="$(process_identity "$preview_pid")"
  if [[ -z "$current_identity" ]] ||
      [[ "$current_identity" != "$recorded_identity" ]]; then
    rm -f "$PID_FILE"
    echo "Removed stale Flex preview PID file for port ${DEV_PORT}."
    return 0
  fi

  kill "$preview_pid"
  for _ in {1..50}; do
    if [[ ! -f "$PID_FILE" ]] ||
        [[ "$(process_identity "$preview_pid")" != "$recorded_identity" ]]; then
      rm -f "$PID_FILE"
      echo "Stopped Flex preview on port ${DEV_PORT}."
      return 0
    fi
    sleep 0.1
  done

  echo "Flex preview on port ${DEV_PORT} did not stop cleanly." >&2
  return 1
}

case "${1:-start}" in
  start)
    ;;
  stop)
    stop_preview
    exit $?
    ;;
  *)
    echo "Usage: $0 [start|stop]" >&2
    exit 2
    ;;
esac

mkdir -p "$STATE_DIR"
if [[ -f "$PID_FILE" ]]; then
  IFS=$'\t' read -r existing_pid existing_identity < "$PID_FILE" || true
  if [[ "$existing_pid" =~ ^[1-9][0-9]*$ ]] &&
      [[ -n "$existing_identity" ]] &&
      [[ "$(process_identity "$existing_pid")" == "$existing_identity" ]]; then
    echo "A Flex preview is already running on port ${DEV_PORT} (PID ${existing_pid})." >&2
    exit 1
  fi
  rm -f "$PID_FILE"
fi
printf '%s\t%s\n' "$$" "$(process_identity "$$")" > "$PID_FILE"

trap cleanup EXIT
trap 'exit 130' INT
trap 'exit 143' TERM

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
