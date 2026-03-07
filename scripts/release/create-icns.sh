#!/usr/bin/env bash
set -euo pipefail

if [[ $# -lt 2 ]]; then
  echo "usage: $0 <input_png> <output_icns>" >&2
  exit 1
fi

INPUT_PNG="$1"
OUTPUT_ICNS="$2"
ICONSET_DIR="$(mktemp -d)/AppIcon.iconset"

mkdir -p "${ICONSET_DIR}"

# 単一PNGから iconset を生成する。
for size in 16 32 64 128 256 512; do
  sips -z "${size}" "${size}" "${INPUT_PNG}" --out "${ICONSET_DIR}/icon_${size}x${size}.png" >/dev/null
  double_size=$((size * 2))
  sips -z "${double_size}" "${double_size}" "${INPUT_PNG}" --out "${ICONSET_DIR}/icon_${size}x${size}@2x.png" >/dev/null
done

iconutil -c icns "${ICONSET_DIR}" -o "${OUTPUT_ICNS}"

echo "icns created: ${OUTPUT_ICNS}"
