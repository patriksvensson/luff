#!/usr/bin/env bash

############################################################################
# Usage: ./teardown.sh [--images]
#   --images   also remove locally built luff-server / luff-agent images
############################################################################

set -euo pipefail

cd "$(dirname "${BASH_SOURCE[0]}")"

remove_images=false
for arg in "$@"; do
  case "$arg" in
    --images) remove_images=true ;;
    *) echo "unknown option: $arg" >&2; exit 2 ;;
  esac
done

echo "==> Removing Luff-deployed app containers"
app_containers=$(docker ps -aq --filter "label=luff.managed=true" || true)
[ -n "$app_containers" ] && docker rm --force --volumes $app_containers || true

echo "==> Removing Luff-deployed app volumes"
app_volumes=$(docker volume ls -q | grep -E '^luff-' || true)
[ -n "$app_volumes" ] && docker volume rm $app_volumes || true

down_args=(--volumes --remove-orphans)
$remove_images && down_args+=(--rmi local)
echo "==> Tearing down base stack (project: luff)"
docker compose down "${down_args[@]}"

echo "==> Done."
