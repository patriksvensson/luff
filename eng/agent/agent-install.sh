#!/usr/bin/env sh
# Luff agent installer. Runs a standalone agent on this host that dials out to an existing control plane
# over the pinned TLS link and holds one connection open. The agent is stateless -- all state and secrets
# live on the control plane -- so this just downloads the compose asset and keeps the container up.
#
# By default the host runs Direct or Internal apps (no Caddy). Pass --front-door to also bring up Caddy and
# host the front door, so the host can serve Web apps. Get --server, --pin and a per-agent --token from the
# control plane's "Add machine" screen.
#
# Re-running upgrades in place: any value you omit is reused from the existing .env, so `--version TAG`
# alone bumps the image.
#
# Usage:   agent-install.sh --name NAME --server https://HOST:8443 --pin PIN --token TOKEN [--front-door] [--dir DIR] [--version TAG]
# Env:     LUFF_HOME  LUFF_VERSION  LUFF_REPO
set -eu

repo="${LUFF_REPO:-patriksvensson/luff}"
version="${LUFF_VERSION:-latest}"
name="${LUFF_AGENT_NAME:-}"
server="${LUFF_SERVER_ADDRESS:-}"
pin="${LUFF_SERVER_PIN:-}"
token="${LUFF_ENROLLMENT_SECRET:-}"
asset="luff-agent-docker.tar.gz"
frontdoor=false
os="$(uname -s)"

case "$os" in
    Darwin) home="${LUFF_HOME:-$HOME/Library/Application Support/Luff Agent}" ;;
    *)      home="${LUFF_HOME:-/opt/luff-agent}" ;;
esac

while [ $# -gt 0 ]; do
    case "$1" in
        --name) name="$2"; shift 2 ;;
        --server) server="$2"; shift 2 ;;
        --pin) pin="$2"; shift 2 ;;
        --token) token="$2"; shift 2 ;;
        --dir) home="$2"; shift 2 ;;
        --version) version="$2"; shift 2 ;;
        --front-door) frontdoor=true; shift ;;
        -h|--help) echo "Usage: agent-install.sh --name NAME --server https://HOST:8443 --pin PIN --token TOKEN [--front-door] [--dir DIR] [--version TAG]"; exit 0 ;;
        *) echo "agent-install.sh: unknown option '$1'" >&2; exit 2 ;;
    esac
done

fail() { echo "agent-install.sh: $1" >&2; exit 1; }

env_file="$home/.env"

# Reuse a value from an existing .env when the flag/env is omitted, so a bare re-run can just upgrade.
existing() {
    [ -f "$env_file" ] || return 0
    grep "^$1=" "$env_file" 2>/dev/null | head -n1 | cut -d= -f2-
}
[ -n "$name" ]   || name="$(existing LUFF_AGENT_NAME)"
[ -n "$server" ] || server="$(existing LUFF_SERVER_ADDRESS)"
[ -n "$pin" ]    || pin="$(existing LUFF_SERVER_PIN)"
[ -n "$token" ]  || token="$(existing LUFF_ENROLLMENT_SECRET)"

[ -n "$name" ]   || fail "missing --name (a unique name for this host)"
[ -n "$server" ] || fail "missing --server (e.g. https://cp.example.ts.net:8443)"
[ -n "$pin" ]    || fail "missing --pin (the control plane's key pin, from Add machine)"
[ -n "$token" ]  || fail "missing --token (mint one on the control plane's Add machine screen)"

if [ "$os" = Darwin ]; then
    [ "$(id -u)" = 0 ] && fail "on macOS run without sudo (Docker Desktop runs as your user, not root)"
else
    [ "$(id -u)" = 0 ] || fail "run as root (needs $home and the docker socket)"
fi
command -v curl >/dev/null 2>&1 || fail "curl is required"
command -v tar  >/dev/null 2>&1 || fail "tar is required"
command -v docker >/dev/null 2>&1 || fail "docker is required"
docker compose version >/dev/null 2>&1 || fail "the docker compose v2 plugin is required"
if ! docker info >/dev/null 2>&1; then
    [ "$os" = Darwin ] && fail "cannot reach the Docker daemon (is Docker Desktop running?)"
    fail "cannot reach the Docker daemon (is dockerd running?)"
fi

if [ "$version" = latest ]; then
    url="https://github.com/$repo/releases/latest/download/$asset"
else
    url="https://github.com/$repo/releases/download/$version/$asset"
fi

tmp="$(mktemp -d)"
trap 'rm -rf "$tmp"' EXIT

echo "Fetching $url"
curl -fsSL "$url" -o "$tmp/$asset" || fail "download failed (is the release public and the asset published?)"
tar -xzf "$tmp/$asset" -C "$tmp"
[ -f "$tmp/compose.yaml" ] || fail "the archive did not contain compose.yaml"

mkdir -p "$home"
cp "$tmp/compose.yaml" "$home/compose.yaml"
if [ -f "$tmp/compose.frontdoor.yaml" ]; then
    cp "$tmp/compose.frontdoor.yaml" "$home/compose.frontdoor.yaml"
elif $frontdoor; then
    fail "the archive has no compose.frontdoor.yaml (need a newer release for --front-door)"
fi

# set_kv KEY VALUE FILE -- replace KEY's line if present, otherwise append it.
set_kv() {
    _tmp="$(mktemp)"
    grep -vE "^$1=" "$3" > "$_tmp" 2>/dev/null || true
    printf '%s=%s\n' "$1" "$2" >> "$_tmp"
    mv "$_tmp" "$3"
}

if [ ! -f "$env_file" ]; then
    cp "$tmp/.env" "$env_file"
else
    new_version="$(grep '^LUFF_VERSION=' "$tmp/.env" | cut -d= -f2- || true)"
    [ -n "$new_version" ] && set_kv LUFF_VERSION "$new_version" "$env_file"
fi

set_kv LUFF_AGENT_NAME "$name" "$env_file"
set_kv LUFF_SERVER_ADDRESS "$server" "$env_file"
set_kv LUFF_SERVER_PIN "$pin" "$env_file"
set_kv LUFF_ENROLLMENT_SECRET "$token" "$env_file"
chmod 600 "$env_file"

cd "$home"
if $frontdoor; then
    set -- -f compose.yaml -f compose.frontdoor.yaml
else
    set -- -f compose.yaml
fi
docker compose "$@" pull
docker compose "$@" up -d

cat <<EOF

Luff agent '$name' is up, dialing $server.
It should appear on the control plane's Machines page within a few seconds.

  Config      $env_file
  Manage      cd "$home" && docker compose logs -f   (or: down / pull && up -d)
EOF
