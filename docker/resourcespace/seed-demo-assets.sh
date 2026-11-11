#!/usr/bin/env bash
set -euo pipefail
export PATH="$PATH:/usr/local/bin:/opt/homebrew/bin:$HOME/.docker/bin"

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
CONTAINER_NAME="${RS_CONTAINER_NAME:-umbraco-resourcespace-dam}"

echo "Copying seeder into ${CONTAINER_NAME}..."
docker cp "${SCRIPT_DIR}/seed-demo-assets.php" "${CONTAINER_NAME}:/tmp/seed-demo-assets.php"

echo "Initializing ResourceSpace schema, API service account, and sample assets..."
docker exec -u www-data "${CONTAINER_NAME}" php /tmp/seed-demo-assets.php
