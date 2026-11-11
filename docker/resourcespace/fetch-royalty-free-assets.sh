#!/usr/bin/env bash
# Downloads CC0 1.0 Public Domain museum artworks from The Art Institute of Chicago
# Open Access IIIF API (https://creativecommons.org/publicdomain/zero/1.0/) into
# docker/resourcespace/seed-assets/ for deterministic local ResourceSpace seeding.

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
OUT_DIR="${SCRIPT_DIR}/seed-assets"
mkdir -p "${OUT_DIR}"

UA="SkyeTechnology-Umbraco-ResourceSpace-Demo/1.0 (https://skye.technology)"

fetch_aic_cc0() {
  local image_id="$1"
  local filename="$2"
  local dest="${OUT_DIR}/${filename}"

  if [ -f "${dest}" ] && [ "$(wc -c < "${dest}")" -gt 15000 ]; then
    echo "[fetch] Already present: ${filename}"
    return 0
  fi

  local url="https://www.artic.edu/iiif/2/${image_id}/full/843,/0/default.jpg"
  echo "[fetch] Downloading CC0 artwork ${filename} (${image_id})..."
  if curl -fsSL -H "User-Agent: ${UA}" -H "AIC-User-Agent: ${UA}" "${url}" -o "${dest}.tmp"; then
    if file "${dest}.tmp" | grep -qi "JPEG"; then
      mv "${dest}.tmp" "${dest}"
      echo "[fetch] Saved ${filename}"
      return 0
    fi
  fi
  rm -f "${dest}.tmp"
  echo "[fetch] ERROR: Could not download ${filename}" >&2
  return 1
}

# 1. Mosaic Floor Panel Depicting Marine Life (Roman, Daphne, 200-230 CE, AIC #23276, CC0)
fetch_aic_cc0 "f59db4d0-7cba-e122-9f1d-5185afc6f6e9" "01-roman-marine-mosaic.jpg"

# 2. Roses in a Bowl (Henri Fantin-Latour, 1881, AIC #20534, CC0)
fetch_aic_cc0 "7abbfd97-fb03-2366-cfe5-b53738a3d93e" "02-botanical-roses-plate.jpg"

# 4. Portrait Head of Emperor Hadrian (Roman Marble, 130-138 CE, AIC #58135, CC0 — seeded with access=1 Restricted)
fetch_aic_cc0 "4ba54d83-416b-92bf-6098-89ffec76a496" "04-hadrian-marble-bust-restricted.jpg"

# 5. Athenian Belly-Amphora Storage Jar (Greek, Athens, 550-540 BCE, AIC #52198, CC0)
fetch_aic_cc0 "890944df-3646-9db0-f1ce-b8321fd40e89" "05-athenian-amphora.jpg"

# 6. The Old Temple (Hubert Robert, 1787-88, AIC #57048, CC0)
fetch_aic_cc0 "00fdacbb-872a-1091-3272-39967374ec07" "06-classical-temple-gallery.jpg"

# 7. Illuminated Manuscript Leaf (Italian, c. 1450, tempera & gold leaf on vellum, AIC #72884, CC0)
fetch_aic_cc0 "53b33d82-e142-bce7-9800-36663c2ef83c" "07-illuminated-manuscript-leaf.jpg"

# 8. Mosaic Fragment with Grazing Camel (Byzantine, 5th century, AIC #35355, CC0)
fetch_aic_cc0 "6f83a79b-cd42-33de-3e15-175642f16f3c" "08-byzantine-mosaic-fragment.jpg"

echo "[fetch] All CC0 museum photography ready in ${OUT_DIR}."
