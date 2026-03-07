#!/usr/bin/env bash
set -euo pipefail

if [[ $# -lt 3 ]]; then
  echo "usage: $0 <app_path> <dmg_output> <volume_name>" >&2
  exit 1
fi

APP_PATH="$1"
DMG_OUTPUT="$2"
VOLUME_NAME="$3"

rm -f "${DMG_OUTPUT}"
hdiutil create \
  -volname "${VOLUME_NAME}" \
  -srcfolder "${APP_PATH}" \
  -ov \
  -format UDZO \
  "${DMG_OUTPUT}"

echo "dmg created: ${DMG_OUTPUT}"
