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
for name in front back left right; do
  source="$VIEW_DIR/$name.jpg"
  if [ ! -s "$source" ]; then
    echo "Missing Madeline production reference: $source" >&2
    echo "See $SCRIPT_DIR/README.md." >&2
    exit 2
  fi
done

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
ImageFile.LOAD_TRUNCATED_IMAGES = False
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

# Derive face identity from the same approved full-resolution front turnaround used
# for reconstruction. This avoids stale/corrupt legacy face assets and guarantees
# that the facial reference matches the current Madeline design. The crop is stored
# as normalized coordinates so the build remains deterministic if the reference is
# re-encoded at another resolution with the same approved framing.
FACE="$OUT/reference/madeline_face_front.png"
"$HUNYUAN_PY" - "$OUT/reference/raw/front.jpg" "$FACE" <<'PY'
from pathlib import Path
import sys
from PIL import Image

source = Path(sys.argv[1])
destination = Path(sys.argv[2])
with Image.open(source) as image:
    image.load()
    rgb = image.convert("RGB")
    width, height = rgb.size
    left = int(round(width * 0.410))
    top = int(round(height * 0.094))
    right = int(round(width * 0.590))
    bottom = int(round(height * 0.214))
    if right - left < 96 or bottom - top < 96:
        raise SystemExit(f"Madeline derived face crop is unexpectedly small: {(left, top, right, bottom)}")
    face = rgb.crop((left, top, right, bottom)).resize((256, 256), Image.Resampling.LANCZOS)
    destination.parent.mkdir(parents=True, exist_ok=True)
    face.save(destination, format="PNG", optimize=False)
with Image.open(destination) as verify:
    verify.load()
    if verify.mode != "RGB" or verify.size != (256, 256):
        raise SystemExit(f"Madeline face output validation failed: mode={verify.mode} size={verify.size}")
print(f"derived face: {source.name} crop=({left},{top})-({right},{bottom}) -> {destination}")
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

printf '%s\n' '[7/9] Restore Madeline facial identity from the approved front turnaround'
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
