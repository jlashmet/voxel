#!/usr/bin/env bash
# Production build for Madeline, the reusable base character behind the Sunlit Cleric.
# Clothing and equipment are intentionally excluded from the generated body.
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/../../../.." && pwd)"
cd "$REPO_ROOT"

BLENDER_BIN="${BLENDER_BIN:-/Applications/Blender.app/Contents/MacOS/Blender}"
test -x "$BLENDER_BIN"

VIEW_DIR="$SCRIPT_DIR/views"
PRECLEAN_DIR="$SCRIPT_DIR/views-bodyonly"
FACE_SOURCE="$SCRIPT_DIR/refs/madeline_face_front.png"
for name in front back left right; do
  source="$VIEW_DIR/$name.jpg"
  if [ ! -s "$source" ]; then
    echo "Missing Madeline production reference: $source" >&2
    echo "See $SCRIPT_DIR/README.md." >&2
    exit 2
  fi
done
if [ ! -s "$FACE_SOURCE" ]; then
  echo "Missing Madeline face reference: $FACE_SOURCE" >&2
  exit 2
fi

CACHE_ROOT="${CHARACTER_FACTORY_CACHE_ROOT:-$HOME/Library/Caches/voxel-character-factory}"
HUNYUAN_REV="f8db63096c8282cb27354314d896feba5ba6ff8a"
HUNYUAN_PY="${HUNYUAN_PY:-$CACHE_ROOT/hunyuan3d-2-$HUNYUAN_REV-venv/bin/python}"
if [ ! -x "$HUNYUAN_PY" ]; then
  HUNYUAN_PY="$(bash "$SCRIPT_DIR/bootstrap_hunyuan_quality_macos.sh" | tail -n 1)"
fi
test -x "$HUNYUAN_PY"

OUT="${1:-$REPO_ROOT/Artifacts/MadelineProduction}"
UNITY_ASSETS_ROOT="${2:-$REPO_ROOT/Assets/Generated/CharacterFactory}"
rm -rf "$OUT"
mkdir -p "$OUT/reference/raw" "$OUT/reference/body-only"

export PYTORCH_ENABLE_MPS_FALLBACK="${PYTORCH_ENABLE_MPS_FALLBACK:-1}"
export PYTHONUNBUFFERED=1
export CHARACTER_FACTORY_ALIGNMENT_BLEND="${CHARACTER_FACTORY_ALIGNMENT_BLEND:-0.15}"

printf '%s\n' '[1/9] Decode, re-encode, and validate the approved Madeline turnaround'
for name in front back left right; do
  "$HUNYUAN_PY" - "$VIEW_DIR/$name.jpg" "$OUT/reference/raw/$name.jpg" <<'PY'
from pathlib import Path
import sys
from PIL import Image, ImageFile

source = Path(sys.argv[1])
destination = Path(sys.argv[2])
ImageFile.LOAD_TRUNCATED_IMAGES = True
image = Image.open(source)
image.load()
rgb = image.convert("RGB")
if rgb.width < 256 or rgb.height < 384:
    raise SystemExit(f"Madeline reference decoded unexpectedly small: {source} -> {rgb.size}")
destination.parent.mkdir(parents=True, exist_ok=True)
rgb.save(destination, format="JPEG", quality=95, subsampling=0)
data = destination.read_bytes()
if len(data) < 4096 or not data.startswith(b"\xff\xd8") or not data.endswith(b"\xff\xd9"):
    raise SystemExit(f"re-encoded Madeline JPEG is invalid: {destination}")
print(f"validated {source.name}: {rgb.width}x{rgb.height}, {len(data)} bytes -> {destination}")
PY
done

# The approved face crop is authoritative identity input. An older copy in Git has
# valid PNG chunk payloads but a bad palette CRC, which Blender/libpng rejects. Repair
# chunk CRCs into the build output, then round-trip through Pillow so downstream tools
# always receive a conventionally encoded RGB PNG without changing the source artwork.
FACE="$OUT/reference/madeline_face_front.png"
"$HUNYUAN_PY" - "$FACE_SOURCE" "$FACE" <<'PY'
from pathlib import Path
import struct
import sys
import zlib
from PIL import Image

source = Path(sys.argv[1])
destination = Path(sys.argv[2])
data = source.read_bytes()
signature = b"\x89PNG\r\n\x1a\n"
if not data.startswith(signature):
    raise SystemExit(f"Madeline face source is not a PNG: {source}")

repaired = bytearray(signature)
offset = len(signature)
saw_iend = False
while offset < len(data):
    if offset + 12 > len(data):
        raise SystemExit(f"Madeline face PNG is truncated at byte {offset}: {source}")
    length = struct.unpack(">I", data[offset : offset + 4])[0]
    chunk_type = data[offset + 4 : offset + 8]
    chunk_end = offset + 12 + length
    if chunk_end > len(data):
        raise SystemExit(
            f"Madeline face PNG chunk {chunk_type!r} exceeds file length: {source}"
        )
    chunk_data = data[offset + 8 : offset + 8 + length]
    repaired.extend(struct.pack(">I", length))
    repaired.extend(chunk_type)
    repaired.extend(chunk_data)
    repaired.extend(struct.pack(">I", zlib.crc32(chunk_type + chunk_data) & 0xFFFFFFFF))
    offset = chunk_end
    if chunk_type == b"IEND":
        saw_iend = True
        break

if not saw_iend:
    raise SystemExit(f"Madeline face PNG has no IEND chunk: {source}")

repaired_path = destination.with_suffix(".crc-repaired.png")
repaired_path.parent.mkdir(parents=True, exist_ok=True)
repaired_path.write_bytes(repaired)
with Image.open(repaired_path) as image:
    image.load()
    rgb = image.convert("RGB")
    if rgb.width < 64 or rgb.height < 64:
        raise SystemExit(f"Madeline face crop decoded unexpectedly small: {rgb.size}")
    rgb.save(destination, format="PNG", optimize=False)
repaired_path.unlink()

with Image.open(destination) as verify:
    verify.load()
    if verify.mode != "RGB":
        raise SystemExit(f"Madeline face output mode is not RGB: {verify.mode}")
print(f"validated face: {rgb.width}x{rgb.height} -> {destination}")
PY

printf '%s\n' '[2/9] Remove the temporary modeling base layer from geometry inputs'
"$HUNYUAN_PY" "$SCRIPT_DIR/prepare_body_texture_views.py" \
  --input-dir "$OUT/reference/raw" \
  --preclean-dir "$PRECLEAN_DIR" \
  --output-dir "$OUT/reference/body-only" \
  --report "$OUT/body-only-reference-report.json"

BODY_FRONT="$OUT/reference/body-only/front.jpg"
BODY_BACK="$OUT/reference/body-only/back.jpg"
BODY_LEFT="$OUT/reference/body-only/left.jpg"
BODY_RIGHT="$OUT/reference/body-only/right.jpg"

printf '%s\n' '[3/9] Ensure the Hunyuan multiview backend is ready'
MODEL_ROOT="${HY3DGEN_MODELS:-$CACHE_ROOT/models}"
export HY3DGEN_MODELS="$MODEL_ROOT"
if [ ! -s "$MODEL_ROOT/tencent/Hunyuan3D-2mv/hunyuan3d-dit-v2-mv-turbo/model.fp16.safetensors" ]; then
  bash "$SCRIPT_DIR/bootstrap_hunyuan_quality_macos.sh" >/dev/null
fi

printf '%s\n' '[4/9] Create the canonical animation skeleton'
CANONICAL="$OUT/canonical_female.glb"
CANONICAL_PREVIEW="$OUT/canonical_female.input.png"
"$BLENDER_BIN" --background --python-exit-code 1 \
  --python tools/character-factory/ci/create_canonical_character_fixture.py -- \
  --canonical "$CANONICAL" \
  --input "$CANONICAL_PREVIEW"
test -s "$CANONICAL"

printf '%s\n' '[5/9] Reconstruct Madeline body geometry from the clothing-free four-view references'
SPEC="$OUT/madeline-body.json"
cat > "$SPEC" <<JSON
{
  "id": "madeline_body_01",
  "assetType": "character",
  "views": {
    "front": "$BODY_FRONT",
    "back": "$BODY_BACK",
    "left": "$BODY_LEFT",
    "right": "$BODY_RIGHT"
  },
  "outputDir": "$OUT",
  "generator": {
    "python": "$HUNYUAN_PY",
    "backend": "hunyuan-pytorch",
    "preset": "quality",
    "model": "tencent/Hunyuan3D-2mv",
    "subfolder": "hunyuan3d-dit-v2-mv-turbo",
    "device": "auto",
    "seed": 31827,
    "steps": 5,
    "octreeResolution": 256,
    "numChunks": 16000,
    "removeBackground": true,
    "enableFlashVdm": false
  },
  "rig": {
    "blender": "$BLENDER_BIN",
    "canonicalBody": "$CANONICAL",
    "bodyObject": "Body",
    "armatureObject": "Armature",
    "maxTransferDistance": 0.45
  }
}
JSON
python3 tools/character-factory/character_factory.py build "$SPEC"
RAW_FBX="$OUT/madeline_body_01.fbx"
test -s "$RAW_FBX"

printf '%s\n' '[6/9] Project body/hair appearance from the approved turnaround'
TEXTURED_FBX="$OUT/madeline_body_01.textured.fbx"
ATLAS="$OUT/madeline_body_01.body_basecolor.png"
"$BLENDER_BIN" --background --python-exit-code 1 \
  --python tools/character-factory/runtime/blender_texture_rigged_character.py -- \
  --input "$RAW_FBX" \
  --output "$TEXTURED_FBX" \
  --front "$BODY_FRONT" \
  --back "$BODY_BACK" \
  --left "$BODY_LEFT" \
  --right "$BODY_RIGHT" \
  --atlas "$ATLAS"
test -s "$TEXTURED_FBX"
test -s "$ATLAS"
mv "$RAW_FBX" "$OUT/madeline_body_01.geometry_only.fbx"
mv "$TEXTURED_FBX" "$RAW_FBX"

printf '%s\n' '[7/9] Restore Madeline facial identity from the original approved face art'
FACE_FBX="$OUT/madeline_body_01.face.fbx"
"$BLENDER_BIN" --background --python-exit-code 1 \
  --python tools/character-factory/runtime/blender_project_face_texture.py -- \
  --input "$RAW_FBX" \
  --face "$FACE" \
  --output "$FACE_FBX"
test -s "$FACE_FBX"
mv "$FACE_FBX" "$RAW_FBX"

printf '%s\n' '[8/9] Verify rig, animation clips, and visual output'
"$BLENDER_BIN" --background --python-exit-code 1 \
  --python tools/character-factory/ci/verify_skinned_character.py -- \
  --input "$RAW_FBX"
"$BLENDER_BIN" --background --python-exit-code 1 \
  --python tools/character-factory/ci/verify_character_animations.py -- \
  --input "$RAW_FBX"

"$BLENDER_BIN" --background --python-exit-code 1 \
  --python tools/character-factory/ci/render_pipeline_artifact.py -- \
  --input "$RAW_FBX" \
  --output "$OUT/madeline_body_01.render.png" \
  --preserve-materials
"$BLENDER_BIN" --background --python-exit-code 1 \
  --python tools/character-factory/ci/render_character_clip.py -- \
  --input "$RAW_FBX" \
  --output "$OUT/madeline_body_01.idle.png" \
  --clip Idle --frame 30

test -s "$OUT/madeline_body_01.render.png"
test -s "$OUT/madeline_body_01.idle.png"

printf '%s\n' '[9/9] Stage the verified base character into Unity'
python3 tools/character-factory/character_factory.py stage-unity \
  "$OUT/manifest.json" \
  --assets-root "$UNITY_ASSETS_ROOT"

printf '%s\n' \
  "Madeline base body built and staged." \
  "Body: $RAW_FBX" \
  "Body atlas: $ATLAS" \
  "Preview: $OUT/madeline_body_01.render.png" \
  "Idle: $OUT/madeline_body_01.idle.png" \
  "Reference audit: $OUT/body-only-reference-report.json" \
  "Alignment blend: $CHARACTER_FACTORY_ALIGNMENT_BLEND" \
  "Unity: $UNITY_ASSETS_ROOT/character/madeline_body_01"
