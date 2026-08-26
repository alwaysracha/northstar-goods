#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
LOCAL_COMPOSE="$ROOT/.tools/docker-compose"

if docker compose version >/dev/null 2>&1; then
  exec docker compose "$@"
fi

if [[ ! -x "$LOCAL_COMPOSE" ]]; then
  mkdir -p "$ROOT/.tools"
  case "$(uname -m)" in
    x86_64) asset="docker-compose-linux-x86_64" ;;
    aarch64|arm64) asset="docker-compose-linux-aarch64" ;;
    *) printf 'Unsupported architecture: %s\n' "$(uname -m)" >&2; exit 1 ;;
  esac
  printf 'Docker Compose plugin not found; downloading a project-local copy...\n' >&2
  curl -fL "https://github.com/docker/compose/releases/latest/download/$asset" -o "$LOCAL_COMPOSE"
  chmod 0755 "$LOCAL_COMPOSE"
fi

exec "$LOCAL_COMPOSE" "$@"
