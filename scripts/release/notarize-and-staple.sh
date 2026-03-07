#!/usr/bin/env bash
set -euo pipefail

if [[ $# -lt 2 ]]; then
  echo "usage: $0 <app_path> <notary_profile>" >&2
  exit 1
fi

APP_PATH="$1"
NOTARY_PROFILE="$2"

xcrun notarytool submit "${APP_PATH}" --keychain-profile "${NOTARY_PROFILE}" --wait
xcrun stapler staple "${APP_PATH}"

echo "notarize & staple completed: ${APP_PATH}"
