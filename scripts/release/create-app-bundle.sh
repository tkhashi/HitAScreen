#!/usr/bin/env bash
set -euo pipefail

if [[ $# -lt 7 ]]; then
  echo "usage: $0 <publish_dir> <app_name> <bundle_id> <version> <executable_name> <icon_icns> <output_dir>" >&2
  exit 1
fi

PUBLISH_DIR="$1"
APP_NAME="$2"
BUNDLE_ID="$3"
VERSION="$4"
EXECUTABLE_NAME="$5"
ICON_PATH="$6"
OUTPUT_DIR="$7"

APP_DIR="${OUTPUT_DIR}/${APP_NAME}.app"
CONTENTS_DIR="${APP_DIR}/Contents"
MACOS_DIR="${CONTENTS_DIR}/MacOS"
RESOURCES_DIR="${CONTENTS_DIR}/Resources"

rm -rf "${APP_DIR}"
mkdir -p "${MACOS_DIR}" "${RESOURCES_DIR}"

cp -R "${PUBLISH_DIR}/." "${MACOS_DIR}/"

if [[ ! -f "${MACOS_DIR}/${EXECUTABLE_NAME}" ]]; then
  echo "実行ファイルが見つかりません: ${MACOS_DIR}/${EXECUTABLE_NAME}" >&2
  exit 1
fi

if [[ -f "${ICON_PATH}" ]]; then
  cp "${ICON_PATH}" "${RESOURCES_DIR}/AppIcon.icns"
fi

cat > "${CONTENTS_DIR}/Info.plist" <<PLIST
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
  <key>CFBundleDevelopmentRegion</key>
  <string>ja</string>
  <key>CFBundleDisplayName</key>
  <string>${APP_NAME}</string>
  <key>CFBundleExecutable</key>
  <string>${EXECUTABLE_NAME}</string>
  <key>CFBundleIconFile</key>
  <string>AppIcon</string>
  <key>CFBundleIdentifier</key>
  <string>${BUNDLE_ID}</string>
  <key>CFBundleInfoDictionaryVersion</key>
  <string>6.0</string>
  <key>CFBundleName</key>
  <string>${APP_NAME}</string>
  <key>CFBundlePackageType</key>
  <string>APPL</string>
  <key>CFBundleShortVersionString</key>
  <string>${VERSION}</string>
  <key>CFBundleVersion</key>
  <string>${VERSION}</string>
  <key>LSMinimumSystemVersion</key>
  <string>13.0</string>
  <key>NSAppleEventsUsageDescription</key>
  <string>HitAScreen の操作対象アプリ制御に使用します。</string>
</dict>
</plist>
PLIST

chmod +x "${MACOS_DIR}/${EXECUTABLE_NAME}"

echo "App bundle created: ${APP_DIR}"
