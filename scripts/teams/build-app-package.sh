#!/usr/bin/env bash
# Build the Microsoft Teams app package ZIP (manifest + icons at archive root).
# No network calls, credentials, or Azure/Teams side effects.
# Never modifies timestamps or metadata of tracked source files under teams/appPackage/.
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
SRC="${ROOT}/teams/appPackage"
OUT_DIR="${ROOT}/build/teams"
OUT_ZIP="${OUT_DIR}/ApprovalGateway.zip"
STAGING=""

fail() {
  echo "error: $*" >&2
  exit 1
}

cleanup() {
  if [[ -n "${STAGING}" && -d "${STAGING}" ]]; then
    rm -rf "${STAGING}"
  fi
}
trap cleanup EXIT

command -v zip >/dev/null 2>&1 || fail "zip is required"
command -v python3 >/dev/null 2>&1 || fail "python3 is required"

[[ -d "${SRC}" ]] || fail "missing app package directory: ${SRC}"

for f in manifest.json color.png outline.png; do
  [[ -f "${SRC}/${f}" ]] || fail "missing required file: ${SRC}/${f}"
done

# Manifest must be valid JSON.
python3 -m json.tool "${SRC}/manifest.json" >/dev/null \
  || fail "manifest.json is not valid JSON"

# Icon dimensions must match current Teams package requirements.
python3 - "${SRC}/color.png" "${SRC}/outline.png" <<'PY'
import struct, sys

def png_size(path):
    with open(path, "rb") as f:
        data = f.read(24)
    if data[:8] != b"\x89PNG\r\n\x1a\n":
        raise SystemExit(f"{path}: not a PNG")
    w, h = struct.unpack(">II", data[16:24])
    return w, h

color, outline = sys.argv[1], sys.argv[2]
cw, ch = png_size(color)
ow, oh = png_size(outline)
if (cw, ch) != (192, 192):
    raise SystemExit(f"{color}: expected 192x192, got {cw}x{ch}")
if (ow, oh) != (32, 32):
    raise SystemExit(f"{outline}: expected 32x32, got {ow}x{oh}")
print(f"icons ok: color={cw}x{ch} outline={ow}x{oh}")
PY

mkdir -p "${OUT_DIR}"
rm -f "${OUT_ZIP}"

# Stage copies only; normalize timestamps on staging, never on tracked sources.
STAGING="$(mktemp -d "${TMPDIR:-/tmp}/teams-app-package.XXXXXX")"
cp "${SRC}/manifest.json" "${SRC}/color.png" "${SRC}/outline.png" "${STAGING}/"

# Deterministic archive: fixed timestamps, sorted members, no extra attributes.
# Files must appear at ZIP root (no teams/appPackage/ prefix).
(
  cd "${STAGING}"
  export TZ=UTC
  touch -t 202001010000 manifest.json color.png outline.png
  zip -X -q "${OUT_ZIP}" manifest.json color.png outline.png
)

# Validate ZIP layout: exactly the three root files.
python3 - "${OUT_ZIP}" <<'PY'
import sys, zipfile

path = sys.argv[1]
expected = {"manifest.json", "color.png", "outline.png"}
with zipfile.ZipFile(path) as zf:
    names = zf.namelist()
if set(names) != expected:
    raise SystemExit(f"unexpected ZIP members: {names!r}; expected {sorted(expected)}")
if any("/" in n or n.startswith("\\") for n in names):
    raise SystemExit(f"ZIP members must be at archive root: {names!r}")
print(f"zip ok: {path}")
print("members:", ", ".join(sorted(names)))
PY

echo "Built ${OUT_ZIP}"
