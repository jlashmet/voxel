#!/usr/bin/env bash
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"
cd "$REPO_ROOT"

BLENDER_BIN="${BLENDER_BIN:-/Applications/Blender.app/Contents/MacOS/Blender}"
UNITY_VERSION="$(awk '/m_EditorVersion:/ { print $2 }' ProjectSettings/ProjectVersion.txt)"
UNITY_BIN="${UNITY_BIN:-/Applications/Unity/Hub/Editor/${UNITY_VERSION}/Unity.app/Contents/MacOS/Unity}"
for required in "$BLENDER_BIN" "$UNITY_BIN"; do
  test -x "$required" || {
    echo "Required executable not found: $required" >&2
    exit 2
  }
done

CHARACTER_ID="${CHARACTER_FACTORY_E2E_CHARACTER_ID:-cf_e2e_hero_01}"
WEAPON_ID="${CHARACTER_FACTORY_E2E_WEAPON_ID:-cf_e2e_weapon_01}"
STAGE_ROOT="${CHARACTER_FACTORY_E2E_ASSETS_ROOT:-Assets/Generated/CharacterFactoryE2E}"
WORK="${CHARACTER_FACTORY_E2E_WORK:-${RUNNER_TEMP:-${TMPDIR:-/tmp}}/character-factory-character-weapon-e2e}"
PROOF="$REPO_ROOT/Artifacts/CharacterFactorySmoke/e2e-character-weapon"
LIBRARY="$WORK/production-assets"
REFERENCE_ROOT="$WORK/references"
CHARACTER_DIR="$LIBRARY/characters/$CHARACTER_ID"
WEAPON_DIR="$LIBRARY/weapons/$WEAPON_ID"
CHARACTER_OUTPUT="$REPO_ROOT/Artifacts/CharacterFactoryProduction/character/$CHARACTER_ID"
WEAPON_OUTPUT="$REPO_ROOT/Artifacts/CharacterFactoryProduction/weapon/$WEAPON_ID"
CHARACTER_MANIFEST="$CHARACTER_OUTPUT/manifest.json"
WEAPON_MANIFEST="$WEAPON_OUTPUT/manifest.json"

rm -rf \
  "$WORK" \
  "$PROOF" \
  "$REPO_ROOT/$STAGE_ROOT" \
  "$CHARACTER_OUTPUT" \
  "$WEAPON_OUTPUT"
mkdir -p "$WORK" "$PROOF" "$REFERENCE_ROOT"

# Keep the installed model/runtime cache, but isolate prepared geometry so this
# proof always executes real image-to-3D generation rather than restoring a prior FBX.
export CHARACTER_FACTORY_GEOMETRY_CACHE_ROOT="$WORK/geometry-cache"
export PYTORCH_ENABLE_MPS_FALLBACK="${PYTORCH_ENABLE_MPS_FALLBACK:-1}"
export PYTHONUNBUFFERED=1

printf '%s\n' '[1/6] Scaffold a fresh character and weapon through the public creation command'
bash tools/character-factory/new_asset_macos.sh \
  character "$CHARACTER_ID" \
  --library-root "$LIBRARY" \
  --tag e2e
bash tools/character-factory/new_asset_macos.sh \
  weapon "$WEAPON_ID" \
  --library-root "$LIBRARY" \
  --tag e2e \
  --socket-bone RightHand

test -s "$CHARACTER_DIR/asset.json"
test -s "$WEAPON_DIR/asset.json"

printf '%s\n' '[2/6] Create character turnaround references and install the weapon source reference'
"$BLENDER_BIN" --background --python-exit-code 1 \
  --python tools/character-factory/ci/create_canonical_character_fixture.py -- \
  --canonical "$REFERENCE_ROOT/reference-character.glb" \
  --input "$REFERENCE_ROOT/character.png" \
  --garment-input "$REFERENCE_ROOT/garment.png"

for view in front back left right; do
  cp "$REFERENCE_ROOT/character.png" "$CHARACTER_DIR/geometry/$view.png"
  cp "$REFERENCE_ROOT/character.png" "$CHARACTER_DIR/appearance/$view.png"
done
cp tools/character-factory/ci/fixtures/sunlit_cleric_staff.jpg \
  "$WEAPON_DIR/geometry/front.jpg"

printf '%s\n' '[3/6] Generate, rig, validate, preview, and stage the new character'
python3 tools/character-factory/character_factory.py produce \
  "$CHARACTER_DIR/asset.json" \
  --unity-assets-root "$STAGE_ROOT"

printf '%s\n' '[4/6] Generate, validate, preview, and stage the new weapon'
python3 tools/character-factory/character_factory.py produce \
  "$WEAPON_DIR/asset.json" \
  --unity-assets-root "$STAGE_ROOT"

python3 - "$CHARACTER_MANIFEST" "$WEAPON_MANIFEST" <<'PY'
import json
from pathlib import Path
import sys

for raw in sys.argv[1:]:
    path = Path(raw)
    assert path.is_file() and path.stat().st_size > 0, path
    payload = json.loads(path.read_text(encoding="utf-8"))
    assert payload["status"] == "complete", payload
    assert payload["geometryCache"]["hit"] is False, payload["geometryCache"]
    output = Path(payload["output"])
    assert output.is_file() and output.stat().st_size > 0, output
PY

printf '%s\n' '[5/6] Import the staged assets in Unity and equip the generated weapon on the generated character'
export CHARACTER_FACTORY_E2E_CHARACTER_ID="$CHARACTER_ID"
export CHARACTER_FACTORY_E2E_WEAPON_ID="$WEAPON_ID"
export CHARACTER_FACTORY_E2E_ASSETS_ROOT="$STAGE_ROOT"
export CHARACTER_FACTORY_E2E_EVIDENCE="$PROOF/unity-evidence.json"
UNITY_LOG="$PROOF/unity-e2e.log"

UNITY_MAX_RSS_MB="${UNITY_MAX_RSS_MB:-14336}" \
UNITY_MAX_MINUTES="${UNITY_MAX_MINUTES:-30}" \
UNITY_BIN="$UNITY_BIN" tools/unity-run.sh \
  -batchmode -nographics -quit \
  -projectPath "$REPO_ROOT" \
  -executeMethod MountingForce.Game.Composition.CharacterEquipment.Editor.CharacterFactoryGeneratedCharacterWeaponVerifier.Verify \
  -logFile "$UNITY_LOG"

test -s "$PROOF/unity-evidence.json"
grep -q 'CHARACTER_FACTORY_CHARACTER_WEAPON_E2E_OK' "$UNITY_LOG"
python3 - "$PROOF/unity-evidence.json" <<'PY'
import json
from pathlib import Path
import sys

payload = json.loads(Path(sys.argv[1]).read_text(encoding="utf-8"))
assert payload["slot"] == "MainHand", payload
assert payload["socket"] == "RightHand", payload
assert payload["equippedWeaponParent"] == "RightHand", payload
assert payload["characterSkinnedRendererCount"] > 0, payload
assert payload["equippedWeaponRendererCount"] > 0, payload
PY

printf '%s\n' '[6/6] Collect generation and Unity proof artifacts'
cp "$CHARACTER_MANIFEST" "$PROOF/character-manifest.json"
cp "$WEAPON_MANIFEST" "$PROOF/weapon-manifest.json"
cp "$CHARACTER_OUTPUT/$CHARACTER_ID.preview.png" "$PROOF/character-preview.png"
cp "$CHARACTER_OUTPUT/$CHARACTER_ID.idle.png" "$PROOF/character-idle.png"
cp "$WEAPON_OUTPUT/$WEAPON_ID.preview.png" "$PROOF/weapon-preview.png"
cp "$CHARACTER_DIR/asset.json" "$PROOF/character-asset.json"
cp "$WEAPON_DIR/asset.json" "$PROOF/weapon-asset.json"

test -s "$PROOF/character-preview.png"
test -s "$PROOF/character-idle.png"
test -s "$PROOF/weapon-preview.png"

echo "CHARACTER_FACTORY_CHARACTER_WEAPON_E2E_OK proof=$PROOF"
