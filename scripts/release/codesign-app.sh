#!/usr/bin/env bash
set -euo pipefail

if [[ $# -lt 3 ]]; then
  echo "usage: $0 <app_path> <codesign_identity> <entitlements_plist>" >&2
  exit 1
fi

APP_PATH="$1"
CODESIGN_IDENTITY="$2"
ENTITLEMENTS_PLIST="$3"

if [[ ! -d "${APP_PATH}" ]]; then
  echo "app not found: ${APP_PATH}" >&2
  exit 1
fi

if [[ ! -f "${ENTITLEMENTS_PLIST}" ]]; then
  echo "entitlements not found: ${ENTITLEMENTS_PLIST}" >&2
  exit 1
fi

# 内部バイナリを先に署名する。
while IFS= read -r -d '' file; do
  codesign --force --options runtime --timestamp --sign "${CODESIGN_IDENTITY}" "${file}"
done < <(find "${APP_PATH}/Contents/MacOS" -type f -perm -111 -print0)

codesign \
  --force \
  --options runtime \
  --entitlements "${ENTITLEMENTS_PLIST}" \
  --timestamp \
  --sign "${CODESIGN_IDENTITY}" \
  "${APP_PATH}"

codesign --verify --deep --strict --verbose=2 "${APP_PATH}"

echo "codesign verification passed: ${APP_PATH}"
