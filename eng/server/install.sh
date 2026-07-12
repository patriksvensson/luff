#!/usr/bin/env sh
# Luff installer for the single-box Docker stack (Caddy front door + control plane + co-located agent).
#
# All persistent state is bind-mounted to a known host directory (default /opt/luff on Linux, or
# ~/Library/Application Support/Luff on macOS) so it survives image updates, container recreation,
# and `docker compose down`:
#   <dir>/data   -> server /data   (luff.db + the keys/ directory: Data-Protection ring + jwt.key)
#   <dir>/caddy  -> caddy  /data   (TLS certificates + the self-signed CA)
# Back up <dir> and you have captured everything.
#
# Re-running upgrades in place: compose.yaml is refreshed and LUFF_VERSION is bumped to the fetched
# release, while .env (secret, domain) and the data directories are left untouched.
#
# Usage:   install.sh [--dir DIR] [--version TAG] [--domain DOMAIN]
# Env:     LUFF_HOME  LUFF_VERSION  LUFF_FRONT_DOOR_DOMAIN  LUFF_REPO
set -eu

os="$(uname -s)"
repo="${LUFF_REPO:-patriksvensson/luff}"
version="${LUFF_VERSION:-latest}"
domain="${LUFF_FRONT_DOOR_DOMAIN:-}"
asset="luff-server-docker.tar.gz"

case "$os" in
    Darwin) home="${LUFF_HOME:-$HOME/Library/Application Support/Luff}" ;;
    *)      home="${LUFF_HOME:-/opt/luff}" ;;
esac

while [ $# -gt 0 ]; do
    case "$1" in
        --dir) home="$2"; shift 2 ;;
        --version) version="$2"; shift 2 ;;
        --domain) domain="$2"; shift 2 ;;
        -h|--help) echo "Usage: install.sh [--dir DIR] [--version TAG] [--domain DOMAIN]"; exit 0 ;;
        *) echo "install.sh: unknown option '$1'" >&2; exit 2 ;;
    esac
done

fail() { echo "install.sh: $1" >&2; exit 1; }

if [ "$os" = Darwin ]; then
    [ "$(id -u)" = 0 ] && fail "on macOS run without sudo (Docker Desktop runs as your user, not root)"
else
    [ "$(id -u)" = 0 ] || fail "run as root (needs $home, ports 80/443, and the docker socket)"
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

mkdir -p "$home" "$home/data" "$home/caddy"
cp "$tmp/compose.yaml" "$home/compose.yaml"
if [ -f "$tmp/uninstall.sh" ]; then
    cp "$tmp/uninstall.sh" "$home/uninstall.sh"
    chmod +x "$home/uninstall.sh"
fi

env_file="$home/.env"

# set_kv KEY VALUE FILE — replace KEY's line if present, otherwise append it.
set_kv() {
    _tmp="$(mktemp)"
    grep -vE "^$1=" "$3" > "$_tmp" 2>/dev/null || true
    printf '%s=%s\n' "$1" "$2" >> "$_tmp"
    mv "$_tmp" "$3"
}

if [ ! -f "$env_file" ]; then
    if [ -z "$domain" ]; then
        if [ "$os" = Darwin ]; then
            domain="127.0.0.1.sslip.io"
        else
            ip="$(curl -fsS --max-time 5 https://api.ipify.org 2>/dev/null || true)"
            if [ -n "$ip" ]; then
                domain="$ip.sslip.io"
            else
                domain="127.0.0.1.sslip.io"
                echo "install.sh: could not detect a public IP; front door left on loopback ($domain)" >&2
            fi
        fi
    fi

    if command -v openssl >/dev/null 2>&1; then
        secret="$(openssl rand -hex 32)"
    else
        secret="$(head -c 32 /dev/urandom | od -An -tx1 | tr -d ' \n')"
    fi

    cp "$tmp/.env" "$env_file"
    set_kv LUFF_ENROLLMENT_SECRET "$secret" "$env_file"
    set_kv LUFF_FRONT_DOOR_DOMAIN "$domain" "$env_file"
    set_kv LUFF_DATA_DIR  "$home/data"  "$env_file"
    set_kv LUFF_CADDY_DIR "$home/caddy" "$env_file"
    chmod 600 "$env_file"
    echo "Wrote $env_file (generated enrollment secret; front door $domain)"
else
    new_version="$(grep '^LUFF_VERSION=' "$tmp/.env" | cut -d= -f2- || true)"
    [ -n "$new_version" ] && set_kv LUFF_VERSION "$new_version" "$env_file"
    echo "Kept existing $env_file (LUFF_VERSION=${new_version:-unchanged})"
fi

cd "$home"
docker compose pull
docker compose up -d

domain="$(grep '^LUFF_FRONT_DOOR_DOMAIN=' "$env_file" | cut -d= -f2-)"
cat <<EOF

Luff is up.
  Dashboard   https://$domain
  Data        $home/data   (luff.db + keys/ -- back up this directory)
  Certs       $home/caddy
  Config      $env_file
  Uninstall   $home/uninstall.sh   (--purge also wipes data)

First login uses the seeded admin account (admin / changeme) and forces a password change.
EOF
