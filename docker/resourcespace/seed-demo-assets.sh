#!/usr/bin/env bash
set -euo pipefail
export PATH="$PATH:/usr/local/bin:/opt/homebrew/bin:$HOME/.docker/bin"

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
CONTAINER_NAME="${RS_CONTAINER_NAME:-umbraco-resourcespace-dam}"
FORCE_RESEED="${RS_FORCE_RESEED:-0}"

if [[ "${1:-}" == "--reseed" || "${1:-}" == "--force" ]]; then
  FORCE_RESEED="1"
fi

# Fetch CC0 museum assets first if seed-assets directory is missing JPEGs
if ! ls "${SCRIPT_DIR}/seed-assets/"*.jpg >/dev/null 2>&1; then
  echo "Bundled CC0 assets not found locally; downloading via fetch-royalty-free-assets.sh..."
  "${SCRIPT_DIR}/fetch-royalty-free-assets.sh"
fi

echo "Copying seeder and royalty-free CC0 museum assets into ${CONTAINER_NAME}..."
docker exec "${CONTAINER_NAME}" mkdir -p /tmp/rs_demo_seed
docker cp "${SCRIPT_DIR}/seed-assets/." "${CONTAINER_NAME}:/tmp/rs_demo_seed/"
docker cp "${SCRIPT_DIR}/seed-demo-assets.php" "${CONTAINER_NAME}:/tmp/seed-demo-assets.php"
docker exec "${CONTAINER_NAME}" chown -R www-data:www-data /tmp/rs_demo_seed /tmp/seed-demo-assets.php

echo "Initializing ResourceSpace schema, API service account, and curated museum assets..."
docker exec -e "RS_FORCE_RESEED=${FORCE_RESEED}" -u www-data "${CONTAINER_NAME}" php /tmp/seed-demo-assets.php
