#!/usr/bin/env sh
# Luff uninstaller for the single-box Docker stack.
#
# Default: stop and remove the stack (containers + network) but KEEP all persistent state
# (<dir>/data, <dir>/caddy, .env, compose.yaml), so a later install.sh resumes where you left off.
#
# Usage:   uninstall.sh [--dir DIR] [--images] [--purge]
#   --images   also remove the pulled ghcr.io/... images
#   --purge    full wipe: named volumes, Luff-deployed app containers + volumes, and the data
#              directories + .env (irreversible; prompts for confirmation)
# Env:     LUFF_HOME
set -eu

home="${LUFF_HOME:-/opt/luff}"
images=false
purge=false

while [ $# -gt 0 ]; do
    case "$1" in
        --dir) home="$2"; shift 2 ;;
        --images) images=true; shift ;;
        --purge) purge=true; shift ;;
        -h|--help) echo "Usage: uninstall.sh [--dir DIR] [--images] [--purge]"; exit 0 ;;
        *) echo "uninstall.sh: unknown option '$1'" >&2; exit 2 ;;
    esac
done

fail() { echo "uninstall.sh: $1" >&2; exit 1; }

[ "$(id -u)" = 0 ] || fail "run as root (needs the docker socket and $home)"
command -v docker >/dev/null 2>&1 || fail "docker is required"
docker compose version >/dev/null 2>&1 || fail "the docker compose v2 plugin is required"
[ -f "$home/compose.yaml" ] || fail "no compose.yaml in $home (use --dir to point at the install)"

if $purge; then
    printf 'WARNING: this deletes all Luff data in %s (database, keys, TLS certs) and removes deployed apps.\n' "$home"
    printf 'Continue? [y/N] '
    read -r reply </dev/tty 2>/dev/null || reply=""
    case "$reply" in
        y|Y|yes|YES) ;;
        *) fail "aborted" ;;
    esac
fi

cd "$home"

# Remove deployed apps first (in their own compose projects) so the shared network detaches cleanly.
if $purge; then
    echo "==> Removing Luff-deployed app containers"
    app_containers="$(docker ps -aq --filter 'label=luff.managed=true' || true)"
    # shellcheck disable=SC2086
    [ -n "$app_containers" ] && docker rm --force --volumes $app_containers >/dev/null || true

    echo "==> Removing Luff-deployed app volumes"
    app_volumes="$(docker volume ls -q | grep -E '^luff-' || true)"
    # shellcheck disable=SC2086
    [ -n "$app_volumes" ] && docker volume rm $app_volumes >/dev/null || true
fi

set -- down --remove-orphans
$images && set -- "$@" --rmi all
$purge && set -- "$@" --volumes

echo "==> Stopping the Luff stack in $home"
docker compose "$@"

if $purge; then
    echo "==> Removing data directories under $home"
    rm -rf "$home/data" "$home/caddy" "$home/.env"
    echo "==> Removed all Luff data. compose.yaml remains -- run 'rm -rf $home' to finish."
else
    echo "==> Stack stopped. Data kept in $home (data/, caddy/, .env) -- re-run install.sh to restore."
fi
